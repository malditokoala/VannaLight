using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

var apply = args.Any(x => string.Equals(x, "--apply", StringComparison.OrdinalIgnoreCase));
var curateMemory = args.Any(x => string.Equals(x, "--curate-memory", StringComparison.OrdinalIgnoreCase));
var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
var apiPath = Path.Combine(repoRoot, "VannaLight.Api");
var userSecretsPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "Microsoft",
    "UserSecrets",
    "90a8f884-35d0-4e5f-a28a-c2509506aa8a",
    "secrets.json");

var configurationBuilder = new ConfigurationBuilder()
    .SetBasePath(apiPath)
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables();

if (File.Exists(userSecretsPath))
{
    configurationBuilder.AddJsonFile(userSecretsPath, optional: true);
}

var configuration = configurationBuilder.Build();

var memoryDbPath = ResolvePath(apiPath, configuration["Paths:Sqlite"] ?? "Data/vanna_memory.db");
var runtimeDbPath = ResolvePath(apiPath, configuration["Paths:RuntimeDb"] ?? "Data/vanna_runtime.db");
var domain = (configuration["Settings:Retrieval:Domain"] ?? "erp-kpi-pilot").Trim();
var environmentName = (configuration["SystemStartup:EnvironmentName"] ?? "Development").Trim();
var defaultProfileKey = (configuration["SystemStartup:DefaultSystemProfile"] ?? "default").Trim();
var productionViewName = ResolveQualifiedObjectName(configuration["KpiViews:ProductionViewName"], "dbo.vw_KpiProduction_v1");
var scrapViewName = ResolveQualifiedObjectName(configuration["KpiViews:ScrapViewName"], "dbo.vw_KpiScrap_v1");
var downtimeViewName = ResolveQualifiedObjectName(configuration["KpiViews:DowntimeViewName"], "dbo.vw_KpiDownTime_v1");

Console.WriteLine($"Runtime DB: {runtimeDbPath}");
Console.WriteLine($"Memory DB:  {memoryDbPath}");
Console.WriteLine($"Domain:     {domain}");
Console.WriteLine();

var trainingExamples = await LoadTrainingExamplesAsync(memoryDbPath);
var trainingByQuestion = trainingExamples
    .GroupBy(x => NormalizeQuestion(x.Question))
    .ToDictionary(
        x => x.Key,
        x => x.OrderByDescending(y => y.IsVerified)
              .ThenByDescending(y => y.Priority)
              .ThenByDescending(y => y.UseCount)
              .First(),
        StringComparer.Ordinal);

var allowedObjects = await LoadAllowedObjectsAsync(memoryDbPath, domain);
var allowedObjectKeys = allowedObjects
    .Select(x => $"{NormalizeObjectName(x.SchemaName)}.{NormalizeObjectName(x.ObjectName)}")
    .Where(x => !string.IsNullOrWhiteSpace(x))
    .ToHashSet(StringComparer.OrdinalIgnoreCase);

var patterns = await LoadPatternsAsync(memoryDbPath, domain);
var patternTerms = await LoadPatternTermsAsync(memoryDbPath, patterns.Select(x => x.Id).ToArray());
var patternTermLookup = patternTerms
    .GroupBy(x => x.PatternId)
    .ToDictionary(x => x.Key, x => (IReadOnlyList<QueryPatternTermRow>)x.ToList());

var operationalConnectionString = await ResolveOperationalConnectionStringAsync(
    memoryDbPath,
    configuration,
    environmentName,
    defaultProfileKey);
var dryRunAvailable = !string.IsNullOrWhiteSpace(operationalConnectionString);

if (curateMemory)
{
    var backupPath = await BackupMemoryDbAsync(memoryDbPath);
    var curation = await CurateMemoryDbAsync(memoryDbPath, domain, allowedObjectKeys, operationalConnectionString);

    Console.WriteLine($"Memory DB backup: {backupPath}");
    Console.WriteLine($"TrainingExamples corregidos por fecha: {curation.ExamplesDateFixed}");
    Console.WriteLine($"TrainingExamples upsertados: {curation.ExamplesUpserted}");
    Console.WriteLine($"QueryPatterns insertados: {curation.PatternsInserted}");
    Console.WriteLine($"QueryPatternTerms insertados: {curation.TermsInserted}");
    Console.WriteLine();

    trainingExamples = await LoadTrainingExamplesAsync(memoryDbPath);
    trainingByQuestion = trainingExamples
        .GroupBy(x => NormalizeQuestion(x.Question))
        .ToDictionary(
            x => x.Key,
            x => x.OrderByDescending(y => y.IsVerified)
                  .ThenByDescending(y => y.Priority)
                  .ThenByDescending(y => y.UseCount)
                  .First(),
            StringComparer.Ordinal);

    patterns = await LoadPatternsAsync(memoryDbPath, domain);
    patternTerms = await LoadPatternTermsAsync(memoryDbPath, patterns.Select(x => x.Id).ToArray());
    patternTermLookup = patternTerms
        .GroupBy(x => x.PatternId)
        .ToDictionary(x => x.Key, x => (IReadOnlyList<QueryPatternTermRow>)x.ToList());
}

var jobs = await LoadQuestionJobsAsync(runtimeDbPath);
var reviewableJobs = jobs
    .Where(x => string.Equals(x.Mode, "Data", StringComparison.OrdinalIgnoreCase))
    .Where(x => !string.IsNullOrWhiteSpace(x.Question))
    .Where(x => !string.IsNullOrWhiteSpace(x.SqlText))
    .OrderBy(x => x.CreatedUtcValue)
    .ToList();

Console.WriteLine($"QuestionJobs total: {jobs.Count}");
Console.WriteLine($"QuestionJobs revisables (Data + SQL): {reviewableJobs.Count}");
Console.WriteLine($"Exact TrainingExamples indexados: {trainingByQuestion.Count}");
Console.WriteLine($"AllowedObjects activos: {allowedObjectKeys.Count}");
Console.WriteLine($"QueryPatterns activos: {patterns.Count}");
Console.WriteLine($"SQL Server dry-run disponible: {dryRunAvailable}");
Console.WriteLine();

var groups = reviewableJobs
    .GroupBy(x => NormalizeQuestion(x.Question))
    .OrderBy(x => x.Min(y => y.CreatedUtcValue))
    .ToList();

var decisions = new List<GroupDecision>(groups.Count);
foreach (var group in groups)
{
    decisions.Add(await ReviewGroupAsync(
        group.Key,
        group.ToList(),
        trainingByQuestion,
        patterns,
        patternTermLookup,
        allowedObjectKeys,
        operationalConnectionString));
}

var verifyable = decisions.Where(x => x.ShouldVerify).ToList();
var unresolved = decisions.Where(x => !x.ShouldVerify).ToList();

Console.WriteLine($"Grupos de pregunta: {groups.Count}");
Console.WriteLine($"Grupos con correccion/verificacion deterministica: {verifyable.Count}");
Console.WriteLine($"Grupos no resueltos: {unresolved.Count}");
Console.WriteLine();

foreach (var item in decisions.Take(120))
{
    Console.WriteLine($"{item.Action} | jobs={item.Jobs.Count} | question={item.Question}");
    if (!string.IsNullOrWhiteSpace(item.Note))
    {
        Console.WriteLine($"  {item.Note}");
    }
}

if (apply)
{
    var backupPath = await BackupRuntimeDbAsync(runtimeDbPath);
    await ApplyDecisionsAsync(runtimeDbPath, verifyable);
    Console.WriteLine();
    Console.WriteLine($"Backup creado: {backupPath}");
    Console.WriteLine($"Rows actualizadas a Verified: {verifyable.Sum(x => x.Jobs.Count)}");
}

return 0;

static async Task<List<QuestionJobRow>> LoadQuestionJobsAsync(string runtimeDbPath)
{
    const string sql = """
        SELECT
            JobId,
            UserId,
            Role,
            Question,
            Status,
            Mode,
            SqlText,
            ErrorText,
            ResultJson,
            Attempt,
            TrainingExampleSaved,
            VerificationStatus,
            FeedbackComment,
            CreatedUtc,
            UpdatedUtc
        FROM QuestionJobs
        ORDER BY CreatedUtc DESC;
        """;

    await using var connection = new SqliteConnection($"Data Source={runtimeDbPath}");
    var rows = await connection.QueryAsync<QuestionJobRow>(sql);
    return rows.ToList();
}

static async Task<List<TrainingExampleRow>> LoadTrainingExamplesAsync(string memoryDbPath)
{
    const string sql = """
        SELECT
            Id,
            Question,
            Sql,
            COALESCE(Domain, '') AS Domain,
            NULLIF(TRIM(COALESCE(IntentName, '')), '') AS IntentName,
            COALESCE(IsVerified, 0) AS IsVerified,
            COALESCE(Priority, 100) AS Priority,
            COALESCE(UseCount, 0) AS UseCount
        FROM TrainingExamples;
        """;

    await using var connection = new SqliteConnection($"Data Source={memoryDbPath}");
    var rows = await connection.QueryAsync<TrainingExampleRow>(sql);
    return rows.ToList();
}

static async Task<List<AllowedObjectRow>> LoadAllowedObjectsAsync(string memoryDbPath, string domain)
{
    const string sql = """
        SELECT
            Id,
            Domain,
            SchemaName,
            ObjectName,
            ObjectType,
            IsActive
        FROM AllowedObjects
        WHERE LOWER(TRIM(Domain)) = @Domain
          AND IsActive = 1;
        """;

    await using var connection = new SqliteConnection($"Data Source={memoryDbPath}");
    var rows = await connection.QueryAsync<AllowedObjectRow>(sql, new { Domain = domain.Trim().ToLowerInvariant() });
    return rows.ToList();
}

static async Task<List<QueryPatternRow>> LoadPatternsAsync(string memoryDbPath, string domain)
{
    const string sql = """
        SELECT
            Id,
            Domain,
            PatternKey,
            IntentName,
            Description,
            SqlTemplate,
            DefaultTopN,
            MetricKey,
            DimensionKey,
            DefaultTimeScopeKey,
            Priority,
            IsActive
        FROM QueryPatterns
        WHERE LOWER(TRIM(Domain)) = @Domain
          AND IsActive = 1
        ORDER BY Priority ASC, Id ASC;
        """;

    await using var connection = new SqliteConnection($"Data Source={memoryDbPath}");
    var rows = await connection.QueryAsync<QueryPatternRow>(sql, new { Domain = domain.Trim().ToLowerInvariant() });
    return rows.ToList();
}

static async Task<List<QueryPatternTermRow>> LoadPatternTermsAsync(string memoryDbPath, long[] patternIds)
{
    if (patternIds.Length == 0)
    {
        return [];
    }

    const string sql = """
        SELECT
            Id,
            PatternId,
            Term,
            TermGroup,
            MatchMode,
            IsRequired,
            IsActive
        FROM QueryPatternTerms
        WHERE PatternId IN @PatternIds
          AND IsActive = 1
        ORDER BY PatternId, IsRequired DESC, Id ASC;
        """;

    await using var connection = new SqliteConnection($"Data Source={memoryDbPath}");
    var rows = await connection.QueryAsync<QueryPatternTermRow>(sql, new { PatternIds = patternIds });
    return rows.ToList();
}

static async Task<string?> ResolveOperationalConnectionStringAsync(
    string memoryDbPath,
    IConfiguration configuration,
    string environmentName,
    string defaultProfileKey)
{
    const string activeSql = """
        SELECT
            Id,
            EnvironmentName,
            ProfileKey,
            ConnectionName,
            ProviderKind,
            ConnectionMode,
            ServerHost,
            DatabaseName,
            UserName,
            IntegratedSecurity,
            Encrypt,
            TrustServerCertificate,
            CommandTimeoutSec,
            SecretRef,
            IsActive
        FROM ConnectionProfiles
        WHERE EnvironmentName = @EnvironmentName
          AND ConnectionName = 'OperationalDb'
          AND IsActive = 1
        LIMIT 1;
        """;

    const string fallbackProfileSql = """
        SELECT
            Id,
            EnvironmentName,
            ProfileKey,
            ConnectionName,
            ProviderKind,
            ConnectionMode,
            ServerHost,
            DatabaseName,
            UserName,
            IntegratedSecurity,
            Encrypt,
            TrustServerCertificate,
            CommandTimeoutSec,
            SecretRef,
            IsActive
        FROM ConnectionProfiles
        WHERE EnvironmentName = @EnvironmentName
          AND ProfileKey = @ProfileKey
          AND ConnectionName = 'OperationalDb'
        LIMIT 1;
        """;

    try
    {
        await using var connection = new SqliteConnection($"Data Source={memoryDbPath}");
        var profile = await connection.QueryFirstOrDefaultAsync<ConnectionProfileRow>(
            activeSql,
            new { EnvironmentName = environmentName });

        profile ??= await connection.QueryFirstOrDefaultAsync<ConnectionProfileRow>(
            fallbackProfileSql,
            new { EnvironmentName = environmentName, ProfileKey = defaultProfileKey });

        if (profile is null)
        {
            var fallback = configuration.GetConnectionString("OperationalDb");
            return string.IsNullOrWhiteSpace(fallback) ? null : fallback;
        }

        if (!string.Equals(profile.ProviderKind, "SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.Equals(profile.ConnectionMode, "FullStringRef", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveSecret(profile.SecretRef, configuration);
        }

        if (!string.Equals(profile.ConnectionMode, "CompositeSqlServer", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(profile.ServerHost) || string.IsNullOrWhiteSpace(profile.DatabaseName))
        {
            return null;
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = profile.ServerHost,
            InitialCatalog = profile.DatabaseName,
            IntegratedSecurity = profile.IntegratedSecurity,
            Encrypt = profile.Encrypt,
            TrustServerCertificate = profile.TrustServerCertificate
        };

        if (!profile.IntegratedSecurity)
        {
            if (string.IsNullOrWhiteSpace(profile.UserName))
            {
                return null;
            }

            var secret = ResolveSecret(profile.SecretRef, configuration);
            if (string.IsNullOrWhiteSpace(secret))
            {
                return null;
            }

            builder.UserID = profile.UserName;
            builder.Password = secret;
        }

        if (profile.CommandTimeoutSec > 0)
        {
            builder.ConnectTimeout = Math.Min(profile.CommandTimeoutSec, 30);
        }

        return builder.ConnectionString;
    }
    catch
    {
        return configuration.GetConnectionString("OperationalDb");
    }
}

static string? ResolveSecret(string? secretRef, IConfiguration configuration)
{
    if (string.IsNullOrWhiteSpace(secretRef))
    {
        return null;
    }

    if (secretRef.StartsWith("env:", StringComparison.OrdinalIgnoreCase))
    {
        return Environment.GetEnvironmentVariable(secretRef[4..]);
    }

    if (secretRef.StartsWith("config:", StringComparison.OrdinalIgnoreCase))
    {
        return configuration[secretRef[7..]];
    }

    return null;
}

static async Task<GroupDecision> ReviewGroupAsync(
    string normalizedQuestion,
    List<QuestionJobRow> jobs,
    IReadOnlyDictionary<string, TrainingExampleRow> trainingByQuestion,
    IReadOnlyList<QueryPatternRow> patterns,
    IReadOnlyDictionary<long, IReadOnlyList<QueryPatternTermRow>> patternTermLookup,
    IReadOnlySet<string> allowedObjectKeys,
    string? operationalConnectionString)
{
    var question = jobs[0].Question?.Trim() ?? string.Empty;
    var notes = new List<string>();
    var validCurrentCandidates = new List<CandidateChoice>();

    foreach (var sqlVariant in jobs
        .Select(x => x.SqlText?.Trim() ?? string.Empty)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct(StringComparer.Ordinal))
    {
        var validation = await ValidateCandidateAsync(sqlVariant, allowedObjectKeys, operationalConnectionString);
        if (validation.IsValid)
        {
            var frequency = jobs.Count(x => string.Equals((x.SqlText ?? string.Empty).Trim(), sqlVariant, StringComparison.Ordinal));
            validCurrentCandidates.Add(new CandidateChoice("CurrentSql", sqlVariant, validation.Note, frequency));
        }
        else
        {
            notes.Add($"SQL actual descartado: {validation.Note}");
        }
    }

    if (trainingByQuestion.TryGetValue(normalizedQuestion, out var exactExample))
    {
        var validation = await ValidateCandidateAsync(exactExample.Sql.Trim(), allowedObjectKeys, operationalConnectionString);
        if (validation.IsValid)
        {
            var suffix = exactExample.IsVerified > 0
                ? "TrainingExample exacto verificado."
                : "TrainingExample exacto no verificado, pero valido.";
            return new GroupDecision(
                question,
                jobs,
                exactExample.Sql.Trim(),
                true,
                "UseExactTrainingExample",
                $"{suffix} {validation.Note}".Trim());
        }

        notes.Add($"TrainingExample exacto descartado: {validation.Note}");
    }

    if (validCurrentCandidates.Count == 1)
    {
        var current = validCurrentCandidates[0];
        if (HasBlockedQuestionIntent(question))
        {
            notes.Add("La pregunta pide una operacion prohibida o metadata; no se marca Verified automaticamente.");
            return new GroupDecision(
                question,
                jobs,
                null,
                false,
                "Unresolved",
                string.Join(" ", notes.Where(x => !string.IsNullOrWhiteSpace(x))));
        }

        return new GroupDecision(
            question,
            jobs,
            current.Sql,
            true,
            "VerifyCurrentSql",
            $"SQL actual valido y deterministico. {current.Note}".Trim());
    }

    if (validCurrentCandidates.Count > 1)
    {
        var groupedByNormalizedSql = validCurrentCandidates
            .GroupBy(x => NormalizeSqlForEquality(x.Sql))
            .OrderByDescending(x => x.Sum(y => y.Frequency))
            .ToList();

        if (groupedByNormalizedSql.Count == 1)
        {
            var current = groupedByNormalizedSql[0].First();
            if (HasBlockedQuestionIntent(question))
            {
                notes.Add("La pregunta pide una operacion prohibida o metadata; no se marca Verified automaticamente.");
                return new GroupDecision(
                    question,
                    jobs,
                    null,
                    false,
                    "Unresolved",
                    string.Join(" ", notes.Where(x => !string.IsNullOrWhiteSpace(x))));
            }

            return new GroupDecision(
                question,
                jobs,
                current.Sql,
                true,
                "VerifyEquivalentCurrentSql",
                $"SQL actual valido en todas sus variantes equivalentes. {current.Note}".Trim());
        }

        notes.Add($"Hay {groupedByNormalizedSql.Count} SQLs actuales validos distintos; se deja sin tocar.");
    }

    var patternMatch = EvaluateBestPattern(question, patterns, patternTermLookup);
    if (patternMatch is not null && patternMatch.IsStrongRouteMatch)
    {
        notes.Add(
            HasPatternRewriteRisk(question)
                ? "Pattern match detectado, pero la pregunta agrega restricciones o matices que no conviene autocorregir con template."
                : "Pattern match detectado, pero se deja para revision manual porque la reescritura vendria de template y no del SQL historico o de un TrainingExample exacto.");
    }

    return new GroupDecision(
        question,
        jobs,
        null,
        false,
        "Unresolved",
        string.Join(" ", notes.Where(x => !string.IsNullOrWhiteSpace(x))));
}

static async Task<ValidationResult> ValidateCandidateAsync(
    string sql,
    IReadOnlySet<string> allowedObjectKeys,
    string? operationalConnectionString)
{
    if (!TryValidateStatically(sql, allowedObjectKeys, out var staticError))
    {
        return new ValidationResult(false, staticError);
    }

    if (string.IsNullOrWhiteSpace(operationalConnectionString))
    {
        return new ValidationResult(true, "Valido estaticamente; dry-run no disponible.");
    }

    var dryRun = await DryRunAsync(operationalConnectionString, sql);
    return dryRun.Ok
        ? new ValidationResult(true, "Valido estaticamente y compila en SQL Server.")
        : new ValidationResult(false, $"No compila en SQL Server: {dryRun.Error}");
}

static bool TryValidateStatically(string sql, IReadOnlySet<string> allowedObjectKeys, out string error)
{
    error = string.Empty;

    if (allowedObjectKeys.Count == 0)
    {
        error = "No hay AllowedObjects activos; validacion fail-closed.";
        return false;
    }

    if (string.IsNullOrWhiteSpace(sql))
    {
        error = "La consulta esta vacia.";
        return false;
    }

    var normalizedSql = NormalizeSqlForValidation(sql);
    normalizedSql = NormalizeLeadingWith(normalizedSql);
    var upperSql = normalizedSql.ToUpperInvariant();

    if (!(upperSql.StartsWith("SELECT", StringComparison.Ordinal) ||
          upperSql.StartsWith("WITH", StringComparison.Ordinal)))
    {
        error = "La consulta debe comenzar con SELECT o WITH.";
        return false;
    }

    if (HasUnexpectedSemicolon(normalizedSql))
    {
        error = "No se permiten multiples statements.";
        return false;
    }

    foreach (var keyword in GetDangerousKeywords())
    {
        if (Regex.IsMatch(upperSql, $@"\b{keyword}\b", RegexOptions.CultureInvariant))
        {
            error = $"Contiene keyword no permitida: {keyword}.";
            return false;
        }
    }

    if (Regex.IsMatch(upperSql, @"\bSELECT\b[\s\S]*\bINTO\b", RegexOptions.CultureInvariant))
    {
        error = "No se permite SELECT INTO.";
        return false;
    }

    if (Regex.IsMatch(upperSql, @"(^|[^A-Z0-9_])#\w+", RegexOptions.CultureInvariant))
    {
        error = "No se permiten tablas temporales.";
        return false;
    }

    if (Regex.IsMatch(upperSql, @"\bSYS\.", RegexOptions.CultureInvariant) ||
        Regex.IsMatch(upperSql, @"\bINFORMATION_SCHEMA\.", RegexOptions.CultureInvariant))
    {
        error = "No se permite consultar metadatos del sistema.";
        return false;
    }

    var cteNames = ExtractCteNames(upperSql);
    var objectMatches = Regex.Matches(
        upperSql,
        @"\b(?:FROM|JOIN)\s+([A-Z0-9_\.\[\]#]+)",
        RegexOptions.CultureInvariant);

    foreach (Match match in objectMatches)
    {
        var rawObject = match.Groups[1].Value;
        var normalizedObject = NormalizeObjectName(rawObject);
        if (string.IsNullOrWhiteSpace(normalizedObject))
        {
            continue;
        }

        if (cteNames.Contains(normalizedObject))
        {
            continue;
        }

        if (!TryBuildAllowedObjectKey(normalizedObject, out var objectKey))
        {
            error = $"No se pudo interpretar el objeto referenciado: {rawObject}.";
            return false;
        }

        if (!allowedObjectKeys.Contains(objectKey))
        {
            error = $"Referencia objeto no permitido: {rawObject}.";
            return false;
        }
    }

    return true;
}

static async Task<(bool Ok, string? Error)> DryRunAsync(string connectionString, string sql)
{
    try
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandTimeout = 5;
        command.CommandText = $"SET NOEXEC ON;\n{sql}\nSET NOEXEC OFF;";

        await command.ExecuteNonQueryAsync();
        return (true, null);
    }
    catch (SqlException ex)
    {
        return (false, ex.Message);
    }
    catch (Exception ex)
    {
        return (false, ex.Message);
    }
}

static PatternEvaluation? EvaluateBestPattern(
    string question,
    IReadOnlyList<QueryPatternRow> patterns,
    IReadOnlyDictionary<long, IReadOnlyList<QueryPatternTermRow>> patternTermLookup)
{
    var normalizedQuestion = NormalizeText(question);
    if (string.IsNullOrWhiteSpace(normalizedQuestion))
    {
        return null;
    }

    PatternEvaluation? best = null;
    foreach (var pattern in patterns)
    {
        if (!patternTermLookup.TryGetValue(pattern.Id, out var terms) || terms.Count == 0)
        {
            continue;
        }

        var evaluation = EvaluatePattern(pattern, terms, normalizedQuestion, question);
        if (evaluation is null)
        {
            continue;
        }

        if (best is null || IsBetter(evaluation, best))
        {
            best = evaluation;
        }
    }

    return best;
}

static PatternEvaluation? EvaluatePattern(
    QueryPatternRow pattern,
    IReadOnlyList<QueryPatternTermRow> terms,
    string normalizedQuestion,
    string rawQuestion)
{
    var requiredTerms = terms.Where(x => x.IsRequired > 0).ToList();
    var matchedTerms = new List<QueryPatternTermRow>(terms.Count);
    var matchedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var exactMatchCount = 0;
    var requiredMatchCount = 0;

    foreach (var term in terms)
    {
        if (!IsTermMatch(normalizedQuestion, term))
        {
            continue;
        }

        matchedTerms.Add(term);
        matchedGroups.Add(NormalizeKey(term.TermGroup));

        if (string.Equals(term.MatchMode, "exact", StringComparison.OrdinalIgnoreCase))
        {
            exactMatchCount++;
        }

        if (term.IsRequired > 0)
        {
            requiredMatchCount++;
        }
    }

    if (matchedTerms.Count == 0)
    {
        return null;
    }

    var requiredSatisfied = requiredTerms.Count == 0 || requiredMatchCount == requiredTerms.Count;
    var optionalMatchCount = matchedTerms.Count - requiredMatchCount;
    var missingRequiredCount = Math.Max(0, requiredTerms.Count - requiredMatchCount);
    var groupCount = matchedGroups.Count;

    var score =
        (requiredMatchCount * 10d) +
        (optionalMatchCount * 4d) +
        (exactMatchCount * 2d) +
        (groupCount * 1.5d) -
        (missingRequiredCount * 8d);

    var hasIntentEvidence =
        score >= 10d &&
        !string.IsNullOrWhiteSpace(pattern.IntentName) &&
        (matchedTerms.Count >= 2 || requiredSatisfied);

    var isStrongRouteMatch =
        SupportsPattern(pattern.PatternKey) &&
        requiredSatisfied &&
        requiredMatchCount > 0 &&
        groupCount >= 2 &&
        score >= 14d;

    return new PatternEvaluation(
        pattern,
        score,
        requiredMatchCount,
        optionalMatchCount,
        hasIntentEvidence,
        isStrongRouteMatch,
        ExtractTopN(rawQuestion),
        ResolveTimeScope(rawQuestion, pattern.DefaultTimeScopeKey));
}

static bool IsTermMatch(string normalizedQuestion, QueryPatternTermRow term)
{
    var normalizedTerm = NormalizeText(term.Term);
    if (string.IsNullOrWhiteSpace(normalizedTerm))
    {
        return false;
    }

    if (string.Equals(term.MatchMode, "exact", StringComparison.OrdinalIgnoreCase))
    {
        return $" {normalizedQuestion} ".Contains($" {normalizedTerm} ", StringComparison.Ordinal);
    }

    return normalizedQuestion.Contains(normalizedTerm, StringComparison.Ordinal);
}

static bool IsBetter(PatternEvaluation candidate, PatternEvaluation current)
{
    var compare = candidate.Score.CompareTo(current.Score);
    if (compare != 0)
    {
        return compare > 0;
    }

    compare = current.Pattern.Priority.CompareTo(candidate.Pattern.Priority);
    if (compare != 0)
    {
        return compare > 0;
    }

    compare = candidate.RequiredMatchCount.CompareTo(current.RequiredMatchCount);
    if (compare != 0)
    {
        return compare > 0;
    }

    compare = candidate.OptionalMatchCount.CompareTo(current.OptionalMatchCount);
    if (compare != 0)
    {
        return compare > 0;
    }

    return candidate.Pattern.Id < current.Pattern.Id;
}

static bool SupportsPattern(string patternKey)
{
    return patternKey switch
    {
        "top_scrap_by_press" => true,
        "total_production" => true,
        "top_downtime_by_failure" => true,
        "top_downtime_by_press" => true,
        "downtime_by_department" => true,
        "top_scrap_cost_by_mold" => true,
        _ => false
    };
}

static string BuildTemplateSql(PatternEvaluation evaluation)
{
    return evaluation.Pattern.PatternKey switch
    {
        "top_scrap_by_press" => BuildTopScrapByPress(evaluation),
        "total_production" => BuildTotalProduction(evaluation),
        "top_downtime_by_failure" => BuildTopDowntimeByFailure(evaluation),
        "top_downtime_by_press" => BuildTopDowntimeByPress(evaluation),
        "downtime_by_department" => BuildDowntimeByDepartment(evaluation),
        "top_scrap_cost_by_mold" => BuildTopScrapCostByMold(evaluation),
        _ => string.Empty
    };
}

static string BuildTopScrapByPress(PatternEvaluation evaluation)
{
    var top = evaluation.ExtractedTopN > 0 ? evaluation.ExtractedTopN : evaluation.Pattern.DefaultTopN.GetValueOrDefault(5);
    return $@"
SELECT TOP ({top})
    s.PressId,
    s.PressName,
    SUM(ISNULL(s.ScrapQty, 0)) AS TotalScrapQty
FROM {scrapViewName} s
WHERE {BuildTimeFilter("s", evaluation.TimeScope, false)}
GROUP BY s.PressId, s.PressName
ORDER BY TotalScrapQty DESC, s.PressName;".Trim();
}

static string BuildTotalProduction(PatternEvaluation evaluation)
{
    return $@"
SELECT
    SUM(ISNULL(p.ProducedQty, 0)) AS TotalProducedQty
FROM {productionViewName} p
WHERE {BuildTimeFilter("p", evaluation.TimeScope, false)};".Trim();
}

static string BuildTopDowntimeByFailure(PatternEvaluation evaluation)
{
    var top = evaluation.ExtractedTopN > 0 ? evaluation.ExtractedTopN : evaluation.Pattern.DefaultTopN.GetValueOrDefault(5);
    return $@"
SELECT TOP ({top})
    d.FailureName,
    SUM(ISNULL(d.DownTimeMinutes, 0)) AS TotalDownTimeMinutes,
    SUM(ISNULL(d.DownTimeCost, 0)) AS TotalDownTimeCost
FROM {downtimeViewName} d
WHERE {BuildTimeFilter("d", evaluation.TimeScope, true)}
GROUP BY d.FailureName
ORDER BY TotalDownTimeMinutes DESC, d.FailureName;".Trim();
}

static string BuildTopDowntimeByPress(PatternEvaluation evaluation)
{
    var top = evaluation.ExtractedTopN > 0 ? evaluation.ExtractedTopN : evaluation.Pattern.DefaultTopN.GetValueOrDefault(5);
    return $@"
SELECT TOP ({top})
    d.PressId,
    d.PressName,
    SUM(ISNULL(d.DownTimeMinutes, 0)) AS TotalDownTimeMinutes,
    SUM(ISNULL(d.DownTimeCost, 0)) AS TotalDownTimeCost
FROM {downtimeViewName} d
WHERE {BuildTimeFilter("d", evaluation.TimeScope, true)}
GROUP BY d.PressId, d.PressName
ORDER BY TotalDownTimeMinutes DESC, d.PressName;".Trim();
}

static string BuildDowntimeByDepartment(PatternEvaluation evaluation)
{
    var topClause = evaluation.ExtractedTopN > 0 ? $"TOP ({evaluation.ExtractedTopN})" : string.Empty;
    return $@"
SELECT {topClause}
    d.DepartmentName,
    SUM(ISNULL(d.DownTimeMinutes, 0)) AS TotalDownTimeMinutes,
    SUM(ISNULL(d.DownTimeCost, 0)) AS TotalDownTimeCost
FROM {downtimeViewName} d
WHERE {BuildTimeFilter("d", evaluation.TimeScope, true)}
GROUP BY d.DepartmentName
ORDER BY TotalDownTimeMinutes DESC, d.DepartmentName;".Trim();
}

static string BuildTopScrapCostByMold(PatternEvaluation evaluation)
{
    var top = evaluation.ExtractedTopN > 0 ? evaluation.ExtractedTopN : evaluation.Pattern.DefaultTopN.GetValueOrDefault(5);
    return $@"
SELECT TOP ({top})
    s.MoldId,
    s.MoldName,
    SUM(ISNULL(s.ScrapCost, 0)) AS TotalScrapCost
FROM {scrapViewName} s
WHERE {BuildTimeFilter("s", evaluation.TimeScope, false)}
GROUP BY s.MoldId, s.MoldName
ORDER BY TotalScrapCost DESC, s.MoldName;".Trim();
}

static string BuildTimeFilter(string alias, PatternTimeScope scope, bool includeIsOpenForDowntime)
{
    var filter = scope switch
    {
        PatternTimeScope.Today => $"CAST({alias}.OperationDate AS date) = CAST(GETDATE() AS date)",
        PatternTimeScope.Yesterday => $"CAST({alias}.OperationDate AS date) = DATEADD(DAY, -1, CAST(GETDATE() AS date))",
        PatternTimeScope.CurrentMonth => $"{alias}.YearMonth = CONVERT(char(7), GETDATE(), 120)",
        PatternTimeScope.CurrentShift => $@"CAST({alias}.OperationDate AS date) = CAST(GETDATE() AS date)
  AND {alias}.ShiftId = (
      SELECT MAX(x.ShiftId)
      FROM {ResolveViewForAlias(alias)} x
      WHERE CAST(x.OperationDate AS date) = CAST(GETDATE() AS date)
  )",
        _ => $"{alias}.YearNumber = YEAR(GETDATE()) AND {alias}.WeekOfYear = DATEPART(ISO_WEEK, GETDATE())"
    };

    if (includeIsOpenForDowntime)
    {
        filter += $"\n  AND {alias}.IsOpen = 0";
    }

    return filter;
}

static string ResolveViewForAlias(string alias)
{
    return alias switch
    {
        "p" => productionViewName,
        "s" => scrapViewName,
        "d" => downtimeViewName,
        _ => productionViewName
    };
}

static string ResolveQualifiedObjectName(string? configuredValue, string fallback)
{
    if (string.IsNullOrWhiteSpace(configuredValue))
        return fallback;

    var trimmed = configuredValue.Trim();
    return Regex.IsMatch(trimmed, @"^[\[\]\w\.]+$") ? trimmed : fallback;
}

static bool HasBlockedQuestionIntent(string question)
{
    var normalized = NormalizeText(question);
    if (string.IsNullOrWhiteSpace(normalized))
    {
        return false;
    }

    return
        normalized.Contains("crea una tabla", StringComparison.Ordinal) ||
        normalized.Contains("crear una tabla", StringComparison.Ordinal) ||
        normalized.Contains("tabla temporal", StringComparison.Ordinal) ||
        normalized.Contains("temp table", StringComparison.Ordinal) ||
        normalized.Contains("pronostico", StringComparison.Ordinal) ||
        normalized.Contains("predic", StringComparison.Ordinal) ||
        normalized.Contains("forecast", StringComparison.Ordinal) ||
        normalized.Contains("sys tables", StringComparison.Ordinal) ||
        normalized.Contains("information schema", StringComparison.Ordinal) ||
        normalized.Contains("usuarios de la base de datos", StringComparison.Ordinal);
}

static bool HasPatternRewriteRisk(string question)
{
    var normalized = NormalizeText(question);
    if (string.IsNullOrWhiteSpace(normalized))
    {
        return false;
    }

    return
        normalized.Contains("menos", StringComparison.Ordinal) ||
        normalized.Contains("sin ", StringComparison.Ordinal) ||
        normalized.Contains("tambien", StringComparison.Ordinal) ||
        normalized.Contains("compar", StringComparison.Ordinal) ||
        normalized.Contains("cliente", StringComparison.Ordinal) ||
        normalized.Contains("operador", StringComparison.Ordinal) ||
        normalized.Contains("molde", StringComparison.Ordinal) ||
        normalized.Contains("eficien", StringComparison.Ordinal) ||
        normalized.Contains("evento", StringComparison.Ordinal) ||
        normalized.Contains("abierto", StringComparison.Ordinal) ||
        normalized.Contains("donde estamos perdiendo", StringComparison.Ordinal);
}

static PatternTimeScope ResolveTimeScope(string question, string? defaultTimeScopeKey)
{
    var explicitScope = ResolveQuestionTimeScope(question);
    return explicitScope != PatternTimeScope.Unknown
        ? explicitScope
        : ParseTimeScope(defaultTimeScopeKey);
}

static PatternTimeScope ResolveQuestionTimeScope(string? question)
{
    var normalized = NormalizeText(question);
    if (string.IsNullOrWhiteSpace(normalized))
    {
        return PatternTimeScope.Unknown;
    }

    if (ContainsAny(normalized, ["turno actual", "del turno", "turno en curso"]))
    {
        return PatternTimeScope.CurrentShift;
    }

    if (ContainsAny(normalized, ["hoy", "dia de hoy", "today"]))
    {
        return PatternTimeScope.Today;
    }

    if (ContainsAny(normalized, ["ayer", "yesterday"]))
    {
        return PatternTimeScope.Yesterday;
    }

    if (ContainsAny(normalized, ["esta semana", "semana actual", "de la semana", "current week"]))
    {
        return PatternTimeScope.CurrentWeek;
    }

    if (ContainsAny(normalized, ["este mes", "mes actual", "del mes", "current month"]))
    {
        return PatternTimeScope.CurrentMonth;
    }

    return PatternTimeScope.Unknown;
}

static bool ContainsAny(string question, IEnumerable<string> values)
{
    return values.Any(value => question.Contains(NormalizeText(value), StringComparison.Ordinal));
}

static PatternTimeScope ParseTimeScope(string? value)
{
    return NormalizeKey(value) switch
    {
        "today" or "hoy" => PatternTimeScope.Today,
        "yesterday" or "ayer" => PatternTimeScope.Yesterday,
        "currentweek" or "thisweek" or "semanaactual" => PatternTimeScope.CurrentWeek,
        "currentmonth" or "thismonth" or "mesactual" => PatternTimeScope.CurrentMonth,
        "currentshift" or "turnoactual" => PatternTimeScope.CurrentShift,
        _ => PatternTimeScope.Unknown
    };
}

static int ExtractTopN(string question)
{
    var match = Regex.Match(question, @"\b(\d{1,3})\b");
    return match.Success && int.TryParse(match.Value, out var value) && value > 0
        ? value
        : 0;
}

static string NormalizeText(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return string.Empty;
    }

    var formD = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
    var sb = new StringBuilder(formD.Length);
    foreach (var c in formD)
    {
        var category = CharUnicodeInfo.GetUnicodeCategory(c);
        if (category == UnicodeCategory.NonSpacingMark)
        {
            continue;
        }

        sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
    }

    return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
}

static string NormalizeQuestion(string? value)
{
    return NormalizeText(value);
}

static string NormalizeKey(string? value)
{
    return NormalizeText(value).Replace(" ", string.Empty, StringComparison.Ordinal);
}

static string NormalizeSqlForValidation(string sql)
{
    var cleaned = sql.Trim();
    cleaned = Regex.Replace(cleaned, @"```sql|```", string.Empty, RegexOptions.IgnoreCase);
    cleaned = Regex.Replace(cleaned, @"--.*?$", string.Empty, RegexOptions.Multiline);
    cleaned = Regex.Replace(cleaned, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
    return cleaned.Trim();
}

static string NormalizeSqlForEquality(string sql)
{
    var normalized = NormalizeSqlForValidation(sql);
    normalized = NormalizeLeadingWith(normalized);
    normalized = Regex.Replace(normalized, @"\s+", " ");
    return normalized.Trim().TrimEnd(';');
}

static string NormalizeLeadingWith(string sql)
{
    var trimmed = sql.Trim();
    return trimmed.StartsWith(";WITH", StringComparison.OrdinalIgnoreCase)
        ? trimmed[1..].TrimStart()
        : trimmed;
}

static bool HasUnexpectedSemicolon(string sql)
{
    var trimmed = sql.Trim();
    if (trimmed.EndsWith(";", StringComparison.Ordinal))
    {
        trimmed = trimmed[..^1].TrimEnd();
    }

    return trimmed.Contains(';');
}

static HashSet<string> ExtractCteNames(string upperSql)
{
    var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    if (!upperSql.StartsWith("WITH", StringComparison.Ordinal))
    {
        return result;
    }

    var matches = Regex.Matches(
        upperSql,
        @"(?:\bWITH\b|,)\s*([A-Z0-9_\[\]]+)\s+AS\s*\(",
        RegexOptions.CultureInvariant);

    foreach (Match match in matches)
    {
        var cteName = NormalizeObjectName(match.Groups[1].Value);
        if (!string.IsNullOrWhiteSpace(cteName))
        {
            result.Add(cteName);
        }
    }

    return result;
}

static bool TryBuildAllowedObjectKey(string normalizedObjectName, out string allowedObjectKey)
{
    allowedObjectKey = string.Empty;
    if (string.IsNullOrWhiteSpace(normalizedObjectName))
    {
        return false;
    }

    var parts = normalizedObjectName
        .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    if (parts.Length == 1)
    {
        allowedObjectKey = $"DBO.{parts[0]}";
        return true;
    }

    if (parts.Length >= 2)
    {
        allowedObjectKey = $"{parts[^2]}.{parts[^1]}";
        return true;
    }

    return false;
}

static string NormalizeObjectName(string value)
{
    return value
        .Replace("[", string.Empty, StringComparison.Ordinal)
        .Replace("]", string.Empty, StringComparison.Ordinal)
        .Trim()
        .ToUpperInvariant();
}

static string ResolvePath(string basePath, string configuredPath)
{
    return Path.IsPathRooted(configuredPath)
        ? configuredPath
        : Path.GetFullPath(Path.Combine(basePath, configuredPath));
}

static string FindRepoRoot(string start)
{
    var dir = new DirectoryInfo(start);
    while (dir is not null)
    {
        if (Directory.Exists(Path.Combine(dir.FullName, "VannaLight.Api")) &&
            Directory.Exists(Path.Combine(dir.FullName, "VannaLight.Core")))
        {
            return dir.FullName;
        }

        dir = dir.Parent;
    }

    throw new InvalidOperationException("Repo root not found.");
}

static async Task<string> BackupRuntimeDbAsync(string runtimeDbPath)
{
    var backupPath = Path.Combine(
        Path.GetDirectoryName(runtimeDbPath) ?? string.Empty,
        $"vanna_runtime.backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.db");

    await using var source = File.Open(runtimeDbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    await using var target = File.Create(backupPath);
    await source.CopyToAsync(target);
    return backupPath;
}

static async Task<string> BackupMemoryDbAsync(string memoryDbPath)
{
    var backupPath = Path.Combine(
        Path.GetDirectoryName(memoryDbPath) ?? string.Empty,
        $"vanna_memory.backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.db");

    await using var source = File.Open(memoryDbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    await using var target = File.Create(backupPath);
    await source.CopyToAsync(target);
    return backupPath;
}

static async Task<MemoryCurationResult> CurateMemoryDbAsync(
    string memoryDbPath,
    string domain,
    IReadOnlySet<string> allowedObjectKeys,
    string? operationalConnectionString)
{
    await using var connection = new SqliteConnection($"Data Source={memoryDbPath}");
    await connection.OpenAsync();
    await using var transaction = await connection.BeginTransactionAsync();

    var exampleRows = await connection.QueryAsync<TrainingExampleSqlRow>(
        new CommandDefinition(
            "SELECT Id, Question, Sql FROM TrainingExamples;",
            transaction: transaction));

    var examplesDateFixed = 0;
    foreach (var row in exampleRows)
    {
        var correctedSql = FixOperationDatePredicates(row.Sql);
        if (string.Equals(correctedSql, row.Sql, StringComparison.Ordinal))
        {
            continue;
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE TrainingExamples SET Sql = @Sql WHERE Id = @Id;",
                new { row.Id, Sql = correctedSql },
                transaction: transaction));
        examplesDateFixed++;
    }

    var curatedExamples = BuildCuratedExamples(domain);
    var examplesUpserted = 0;
    foreach (var example in curatedExamples)
    {
        var validation = await ValidateCandidateAsync(example.Sql, allowedObjectKeys, operationalConnectionString);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                $"Curated TrainingExample invalido para '{example.Question}': {validation.Note}");
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                @"
UPDATE TrainingExamples
SET Sql = @Sql,
    Domain = @Domain,
    IntentName = @IntentName,
    IsVerified = @IsVerified,
    Priority = CASE WHEN @Priority > COALESCE(Priority, 0) THEN @Priority ELSE COALESCE(Priority, 0) END,
    LastUsedUtc = @Now
WHERE Question = @Question;

INSERT INTO TrainingExamples
    (Question, Sql, Domain, IntentName, IsVerified, Priority, CreatedUtc, LastUsedUtc, UseCount)
SELECT
    @Question,
    @Sql,
    @Domain,
    @IntentName,
    @IsVerified,
    @Priority,
    @Now,
    @Now,
    0
WHERE NOT EXISTS (
    SELECT 1
    FROM TrainingExamples
    WHERE Question = @Question
);",
                new
                {
                    example.Question,
                    example.Sql,
                    Domain = example.Domain,
                    IntentName = example.IntentName,
                    IsVerified = example.IsVerified ? 1 : 0,
                    example.Priority,
                    Now = DateTime.UtcNow
                },
                transaction: transaction));

        examplesUpserted++;
    }

    var patternsInserted = 0;
    var termsInserted = 0;

    var patternId = await connection.ExecuteScalarAsync<long?>(
        new CommandDefinition(
            @"
SELECT Id
FROM QueryPatterns
WHERE LOWER(TRIM(Domain)) = @Domain
  AND LOWER(TRIM(PatternKey)) = LOWER(TRIM(@PatternKey))
LIMIT 1;",
            new
            {
                Domain = domain.Trim().ToLowerInvariant(),
                PatternKey = "top_downtime_by_press"
            },
            transaction: transaction));

    if (!patternId.HasValue || patternId.Value <= 0)
    {
        const string sql = @"
INSERT INTO QueryPatterns
(
    Domain,
    PatternKey,
    IntentName,
    Description,
    SqlTemplate,
    DefaultTopN,
    MetricKey,
    DimensionKey,
    DefaultTimeScopeKey,
    Priority,
    IsActive,
    CreatedUtc,
    UpdatedUtc
)
VALUES
(
    @Domain,
    @PatternKey,
    @IntentName,
    @Description,
    @SqlTemplate,
    @DefaultTopN,
    @MetricKey,
    @DimensionKey,
    @DefaultTimeScopeKey,
    @Priority,
    1,
    @Now,
    NULL
);
SELECT last_insert_rowid();";

        patternId = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                sql,
                new
                {
                    Domain = domain.Trim().ToLowerInvariant(),
                    PatternKey = "top_downtime_by_press",
                    IntentName = "Downtime por prensa",
                    Description = "Top N prensas con mayor downtime acumulado para el scope solicitado.",
                    SqlTemplate = "SELECT TOP ({topN}) ... SUM(DownTimeMinutes) GROUP BY PressId, PressName ORDER BY TotalDownTimeMinutes DESC",
                    DefaultTopN = 5,
                    MetricKey = "DownTimeMinutes",
                    DimensionKey = "Press",
                    DefaultTimeScopeKey = "currentShift",
                    Priority = 90,
                    Now = DateTime.UtcNow
                },
                transaction: transaction));

        patternsInserted++;
    }

    foreach (var term in BuildDowntimeByPressTerms(patternId.Value))
    {
        var affected = await connection.ExecuteScalarAsync<long?>(
            new CommandDefinition(
                @"
SELECT Id
FROM QueryPatternTerms
WHERE PatternId = @PatternId
  AND LOWER(TRIM(TermGroup)) = LOWER(TRIM(@TermGroup))
  AND LOWER(TRIM(Term)) = LOWER(TRIM(@Term))
LIMIT 1;",
                new
                {
                    term.PatternId,
                    term.TermGroup,
                    term.Term
                },
                transaction: transaction));

        if (affected.HasValue && affected.Value > 0)
        {
            continue;
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                @"
INSERT INTO QueryPatternTerms
(
    PatternId,
    Term,
    TermGroup,
    MatchMode,
    IsRequired,
    IsActive,
    CreatedUtc
)
VALUES
(
    @PatternId,
    @Term,
    @TermGroup,
    @MatchMode,
    @IsRequired,
    1,
    @Now
);",
                new
                {
                    term.PatternId,
                    term.Term,
                    term.TermGroup,
                    term.MatchMode,
                    IsRequired = term.IsRequired ? 1 : 0,
                    Now = DateTime.UtcNow
                },
                transaction: transaction));

        termsInserted++;
    }

    await transaction.CommitAsync();
    return new MemoryCurationResult(examplesDateFixed, examplesUpserted, patternsInserted, termsInserted);
}

static async Task ApplyDecisionsAsync(string runtimeDbPath, IReadOnlyList<GroupDecision> decisions)
{
    const string sql = """
        UPDATE QuestionJobs
        SET SqlText = @SqlText,
            TrainingExampleSaved = 1,
            VerificationStatus = 'Verified',
            FeedbackComment = @FeedbackComment,
            UpdatedUtc = DATETIME('now')
        WHERE JobId = @JobId;
        """;

    await using var connection = new SqliteConnection($"Data Source={runtimeDbPath}");
    await connection.OpenAsync();
    await using var transaction = await connection.BeginTransactionAsync();

    foreach (var decision in decisions)
    {
        foreach (var job in decision.Jobs)
        {
            await connection.ExecuteAsync(
                sql,
                new
                {
                    JobId = job.JobId,
                    SqlText = decision.CorrectedSql,
                    FeedbackComment = decision.Note
                },
                transaction);
        }
    }

    await transaction.CommitAsync();
}

static string FixOperationDatePredicates(string sql)
{
    if (string.IsNullOrWhiteSpace(sql))
    {
        return sql;
    }

    return Regex.Replace(
        sql,
        @"(?<!CAST\()(\b[a-zA-Z_][a-zA-Z0-9_]*\.OperationDate)\s*=\s*CAST\(GETDATE\(\)\s+AS\s+date\)",
        "CAST($1 AS date) = CAST(GETDATE() AS date)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}

static IReadOnlyList<CuratedTrainingExample> BuildCuratedExamples(string domain)
{
    return
    [
        new CuratedTrainingExample(
            "¿Qué prensas llevan más tiempo caído en el turno actual?",
            $$"""
WITH CurrentShift AS (
    SELECT MAX(d.ShiftId) AS ShiftId
    FROM {downtimeViewName} d
    WHERE CAST(d.OperationDate AS date) = CAST(GETDATE() AS date)
)
SELECT TOP (5)
    d.PressId,
    d.PressName,
    SUM(ISNULL(d.DownTimeMinutes, 0)) AS TotalDownTimeMinutes,
    SUM(ISNULL(d.DownTimeCost, 0)) AS TotalDownTimeCost
FROM {downtimeViewName} d
INNER JOIN CurrentShift cs
    ON d.ShiftId = cs.ShiftId
WHERE CAST(d.OperationDate AS date) = CAST(GETDATE() AS date)
  AND d.IsOpen = 0
GROUP BY d.PressId, d.PressName
ORDER BY TotalDownTimeMinutes DESC, d.PressName;
""".Trim(),
            domain,
            "Downtime por prensa",
            true,
            30),
        new CuratedTrainingExample(
            "¿Cuánto producimos hoy en total?",
            $$"""
SELECT
    SUM(ISNULL(p.ProducedQty, 0)) AS TotalProducedQty
FROM {productionViewName} p
WHERE CAST(p.OperationDate AS date) = CAST(GETDATE() AS date);
""".Trim(),
            domain,
            "Producción total",
            true,
            28),
        new CuratedTrainingExample(
            "¿Cuáles fueron las 5 prensas con mayor volumen de producción real el día de ayer?",
            $$"""
SELECT TOP (5)
    p.PressId,
    p.PressName,
    SUM(ISNULL(p.ProducedQty, 0)) AS TotalProducedQty
FROM {productionViewName} p
WHERE CAST(p.OperationDate AS date) = DATEADD(DAY, -1, CAST(GETDATE() AS date))
GROUP BY p.PressId, p.PressName
ORDER BY TotalProducedQty DESC, p.PressName;
""".Trim(),
            domain,
            null,
            true,
            20),
        new CuratedTrainingExample(
            "¿Qué prensas generaron menos scrap hoy?",
            $$"""
SELECT
    s.PressId,
    s.PressName,
    SUM(ISNULL(s.ScrapQty, 0)) AS TotalScrapQty
FROM {scrapViewName} s
WHERE CAST(s.OperationDate AS date) = CAST(GETDATE() AS date)
GROUP BY s.PressId, s.PressName
ORDER BY TotalScrapQty ASC, s.PressName;
""".Trim(),
            domain,
            null,
            true,
            18),
        new CuratedTrainingExample(
            "¿Qué cliente acumuló más valor de producción este mes?",
            $$"""
SELECT TOP (1)
    p.CustomerId,
    p.CustomerName,
    SUM(ISNULL(p.ProductionValue, 0)) AS TotalProductionValue
FROM {productionViewName} p
WHERE p.YearMonth = CONVERT(char(7), GETDATE(), 120)
GROUP BY p.CustomerId, p.CustomerName
ORDER BY TotalProductionValue DESC, p.CustomerName;
""".Trim(),
            domain,
            null,
            true,
            22),
        new CuratedTrainingExample(
            "¿Qué moldes tuvieron más tiempo caído hoy y cuántos minutos acumularon?",
            $$"""
SELECT TOP (10)
    d.MoldId,
    MAX(d.MoldName) AS MoldName,
    SUM(ISNULL(d.DownTimeMinutes, 0)) AS TotalDownTimeMinutes,
    SUM(ISNULL(d.DownTimeCost, 0)) AS TotalDownTimeCost
FROM {downtimeViewName} d
WHERE CAST(d.OperationDate AS date) = CAST(GETDATE() AS date)
  AND d.IsOpen = 0
GROUP BY d.MoldId
ORDER BY TotalDownTimeMinutes DESC, MoldName;
""".Trim(),
            domain,
            null,
            true,
            22),
        new CuratedTrainingExample(
            "¿Qué prensas con producción hoy también tuvieron scrap hoy, y cuánto scrap generó cada una?",
            $$"""
WITH ProductionToday AS (
    SELECT
        p.PressId,
        MAX(p.PressName) AS PressName,
        SUM(ISNULL(p.ProducedQty, 0)) AS TotalProducedQty
    FROM {productionViewName} p
    WHERE CAST(p.OperationDate AS date) = CAST(GETDATE() AS date)
    GROUP BY p.PressId
),
ScrapToday AS (
    SELECT
        s.PressId,
        SUM(ISNULL(s.ScrapQty, 0)) AS TotalScrapQty,
        SUM(ISNULL(s.ScrapCost, 0)) AS TotalScrapCost
    FROM {scrapViewName} s
    WHERE CAST(s.OperationDate AS date) = CAST(GETDATE() AS date)
    GROUP BY s.PressId
)
SELECT
    p.PressId,
    p.PressName,
    p.TotalProducedQty,
    s.TotalScrapQty,
    s.TotalScrapCost
FROM ProductionToday p
INNER JOIN ScrapToday s
    ON s.PressId = p.PressId
ORDER BY s.TotalScrapQty DESC, p.TotalProducedQty DESC, p.PressName;
""".Trim(),
            domain,
            null,
            true,
            24),
        new CuratedTrainingExample(
            "¿Dónde estamos perdiendo más dinero este mes: en scrap o en tiempo caído?",
            $$"""
WITH ScrapByPress AS (
    SELECT
        s.PressId,
        MAX(s.PressName) AS PressName,
        SUM(ISNULL(s.ScrapCost, 0)) AS TotalScrapCost
    FROM {scrapViewName} s
    WHERE s.YearMonth = CONVERT(char(7), GETDATE(), 120)
    GROUP BY s.PressId
),
DownTimeByPress AS (
    SELECT
        d.PressId,
        MAX(d.PressName) AS PressName,
        SUM(ISNULL(d.DownTimeCost, 0)) AS TotalDownTimeCost
    FROM {downtimeViewName} d
    WHERE d.YearMonth = CONVERT(char(7), GETDATE(), 120)
      AND d.IsOpen = 0
    GROUP BY d.PressId
)
SELECT TOP (10)
    COALESCE(s.PressId, d.PressId) AS PressId,
    COALESCE(s.PressName, d.PressName) AS PressName,
    ISNULL(s.TotalScrapCost, 0) AS TotalScrapCost,
    ISNULL(d.TotalDownTimeCost, 0) AS TotalDownTimeCost,
    ISNULL(s.TotalScrapCost, 0) + ISNULL(d.TotalDownTimeCost, 0) AS TotalLossCost
FROM ScrapByPress s
FULL OUTER JOIN DownTimeByPress d
    ON d.PressId = s.PressId
ORDER BY TotalLossCost DESC, PressName;
""".Trim(),
            domain,
            null,
            true,
            24),
        new CuratedTrainingExample(
            "Muéstrame el top 5 de números de parte con menor eficiencia en la última semana.",
            $$"""
SELECT TOP (5)
    p.PartId,
    p.PartNumber,
    p.PartName,
    SUM(ISNULL(p.ProducedQty, 0)) AS TotalProducedQty,
    SUM(ISNULL(p.TargetQty, 0)) AS TotalTargetQty,
    CAST(
        100.0 * SUM(ISNULL(p.ProducedQty, 0))
        / NULLIF(SUM(ISNULL(p.TargetQty, 0)), 0)
        AS DECIMAL(18,2)
    ) AS EfficiencyPct
FROM {productionViewName} p
WHERE CAST(p.OperationDate AS date) >= DATEADD(DAY, -6, CAST(GETDATE() AS date))
  AND CAST(p.OperationDate AS date) <= CAST(GETDATE() AS date)
GROUP BY p.PartId, p.PartNumber, p.PartName
HAVING SUM(ISNULL(p.TargetQty, 0)) > 0
ORDER BY EfficiencyPct ASC, p.PartNumber;
""".Trim(),
            domain,
            null,
            true,
            20),
        new CuratedTrainingExample(
            "Muéstrame el top 5 de números de parte con mayor eficiencia en la última semana.",
            $$"""
SELECT TOP (5)
    p.PartId,
    p.PartNumber,
    p.PartName,
    SUM(ISNULL(p.ProducedQty, 0)) AS TotalProducedQty,
    SUM(ISNULL(p.TargetQty, 0)) AS TotalTargetQty,
    CAST(
        100.0 * SUM(ISNULL(p.ProducedQty, 0))
        / NULLIF(SUM(ISNULL(p.TargetQty, 0)), 0)
        AS DECIMAL(18,2)
    ) AS EfficiencyPct
FROM {productionViewName} p
WHERE CAST(p.OperationDate AS date) >= DATEADD(DAY, -6, CAST(GETDATE() AS date))
  AND CAST(p.OperationDate AS date) <= CAST(GETDATE() AS date)
GROUP BY p.PartId, p.PartNumber, p.PartName
HAVING SUM(ISNULL(p.TargetQty, 0)) > 0
ORDER BY EfficiencyPct DESC, p.PartNumber;
""".Trim(),
            domain,
            null,
            true,
            20),
        new CuratedTrainingExample(
            "Cuáles son las 3 prensas con más scrap de la semana?",
            $$"""
SELECT TOP (3)
    s.PressId,
    s.PressName,
    SUM(ISNULL(s.ScrapQty, 0)) AS TotalScrapQty
FROM {scrapViewName} s
WHERE s.YearNumber = YEAR(GETDATE())
  AND s.WeekOfYear = DATEPART(ISO_WEEK, GETDATE())
GROUP BY s.PressId, s.PressName
ORDER BY TotalScrapQty DESC, s.PressName;
""".Trim(),
            domain,
            "Top scrap por prensa",
            true,
            26)
    ];
}

static IReadOnlyList<CuratedPatternTerm> BuildDowntimeByPressTerms(long patternId)
{
    return
    [
        new CuratedPatternTerm(patternId, "downtime", "metric", "contains", true),
        new CuratedPatternTerm(patternId, "tiempo caido", "metric", "contains", false),
        new CuratedPatternTerm(patternId, "prensa", "dimension", "contains", true),
        new CuratedPatternTerm(patternId, "prensas", "dimension", "contains", false),
        new CuratedPatternTerm(patternId, "top", "qualifier", "contains", false),
        new CuratedPatternTerm(patternId, "mas", "qualifier", "contains", false),
        new CuratedPatternTerm(patternId, "mayor", "qualifier", "contains", false)
    ];
}

static string[] GetDangerousKeywords()
{
    return
    [
        "INSERT", "UPDATE", "DELETE", "DROP", "TRUNCATE",
        "ALTER", "CREATE", "EXEC", "EXECUTE", "MERGE",
        "OPENROWSET", "OPENDATASOURCE", "BULK"
    ];
}

internal sealed record QuestionJobRow
{
    public string JobId { get; init; } = string.Empty;
    public string? UserId { get; init; }
    public string? Role { get; init; }
    public string? Question { get; init; }
    public string? Status { get; init; }
    public string? Mode { get; init; }
    public string? SqlText { get; init; }
    public string? ErrorText { get; init; }
    public string? ResultJson { get; init; }
    public int Attempt { get; init; }
    public int TrainingExampleSaved { get; init; }
    public string? VerificationStatus { get; init; }
    public string? FeedbackComment { get; init; }
    public string? CreatedUtc { get; init; }
    public string? UpdatedUtc { get; init; }

    public DateTime CreatedUtcValue => DateTime.TryParse(CreatedUtc, out var value) ? value : DateTime.MinValue;
}

internal sealed record TrainingExampleRow
{
    public long Id { get; init; }
    public string Question { get; init; } = string.Empty;
    public string Sql { get; init; } = string.Empty;
    public string Domain { get; init; } = string.Empty;
    public string? IntentName { get; init; }
    public int IsVerified { get; init; }
    public int Priority { get; init; }
    public int UseCount { get; init; }
}

internal sealed record AllowedObjectRow
{
    public long Id { get; init; }
    public string Domain { get; init; } = string.Empty;
    public string SchemaName { get; init; } = string.Empty;
    public string ObjectName { get; init; } = string.Empty;
    public string ObjectType { get; init; } = string.Empty;
    public int IsActive { get; init; }
}

internal sealed record QueryPatternRow
{
    public long Id { get; init; }
    public string Domain { get; init; } = string.Empty;
    public string PatternKey { get; init; } = string.Empty;
    public string IntentName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string SqlTemplate { get; init; } = string.Empty;
    public int? DefaultTopN { get; init; }
    public string? MetricKey { get; init; }
    public string? DimensionKey { get; init; }
    public string? DefaultTimeScopeKey { get; init; }
    public int Priority { get; init; }
    public int IsActive { get; init; }
}

internal sealed record QueryPatternTermRow
{
    public long Id { get; init; }
    public long PatternId { get; init; }
    public string Term { get; init; } = string.Empty;
    public string TermGroup { get; init; } = string.Empty;
    public string MatchMode { get; init; } = "contains";
    public int IsRequired { get; init; }
    public int IsActive { get; init; }
}

internal sealed record ConnectionProfileRow
{
    public long Id { get; init; }
    public string EnvironmentName { get; init; } = string.Empty;
    public string ProfileKey { get; init; } = string.Empty;
    public string ConnectionName { get; init; } = string.Empty;
    public string ProviderKind { get; init; } = string.Empty;
    public string ConnectionMode { get; init; } = string.Empty;
    public string? ServerHost { get; init; }
    public string? DatabaseName { get; init; }
    public string? UserName { get; init; }
    public bool IntegratedSecurity { get; init; }
    public bool Encrypt { get; init; }
    public bool TrustServerCertificate { get; init; }
    public int CommandTimeoutSec { get; init; }
    public string? SecretRef { get; init; }
    public int IsActive { get; init; }
}

internal sealed record CandidateChoice(string Source, string Sql, string Note, int Frequency);
internal sealed record GroupDecision(string Question, IReadOnlyList<QuestionJobRow> Jobs, string? CorrectedSql, bool ShouldVerify, string Action, string Note);
internal sealed record ValidationResult(bool IsValid, string Note);
internal sealed record MemoryCurationResult(int ExamplesDateFixed, int ExamplesUpserted, int PatternsInserted, int TermsInserted);
internal sealed record CuratedTrainingExample(string Question, string Sql, string Domain, string? IntentName, bool IsVerified, int Priority);
internal sealed record CuratedPatternTerm(long PatternId, string Term, string TermGroup, string MatchMode, bool IsRequired);
internal sealed record TrainingExampleSqlRow(long Id, string Question, string Sql);

internal sealed record PatternEvaluation(
    QueryPatternRow Pattern,
    double Score,
    int RequiredMatchCount,
    int OptionalMatchCount,
    bool HasIntentEvidence,
    bool IsStrongRouteMatch,
    int ExtractedTopN,
    PatternTimeScope TimeScope);

internal enum PatternTimeScope
{
    Unknown = 0,
    Today = 1,
    Yesterday = 2,
    CurrentWeek = 3,
    CurrentMonth = 4,
    CurrentShift = 5
}
