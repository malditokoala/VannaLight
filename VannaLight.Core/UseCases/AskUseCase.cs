using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;
using VannaLight.Core.Settings;

namespace VannaLight.Core.UseCases;

public enum AskFailureKind
{
    None = 0,
    GenerationError = 1,
    ValidationError = 2,
    DryRunError = 3
}

public record AskResult(
    bool Success,
    string Sql,
    string? Error,
    bool PassedDryRun,
    AskFailureKind FailureKind = AskFailureKind.None,
    long? ReviewId = null,
    string? ResultJson = null);

public class AskUseCase(
    IRetriever retriever,
    ITrainingStore trainingStore,
    ILlmClient llmClient,
    ISqlValidator validator,
    ISqlDryRunner dryRunner,
    IReviewStore reviewStore,
    AppSettings settings,
    IPredictionIntentRouter predictionRouter,
    IForecastingService forecaster,
    IPredictionAnswerService humanizer,
    ISystemConfigProvider systemConfigProvider,
    IBusinessRuleStore businessRuleStore,
    ISemanticHintStore semanticHintStore,
    IPatternMatcherService patternMatcher,
    ITemplateSqlBuilder templateSqlBuilder,
    IAllowedObjectStore allowedObjectStore)
{
    public async Task<AskResult> ExecuteAsync(
        string question,
        string memoryDbPath,
        string runtimeDbPath,
        string sqlServerConnString,
        AskExecutionContext executionContext,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return new AskResult(
                false,
                string.Empty,
                "La pregunta esta vacia.",
                false,
                AskFailureKind.GenerationError);
        }

        if (string.IsNullOrWhiteSpace(memoryDbPath))
        {
            return new AskResult(
                false,
                string.Empty,
                "No se configuro la base de memoria.",
                false,
                AskFailureKind.GenerationError);
        }

        if (string.IsNullOrWhiteSpace(runtimeDbPath))
        {
            return new AskResult(
                false,
                string.Empty,
                "No se configuro la base de runtime.",
                false,
                AskFailureKind.GenerationError);
        }

        if (executionContext is null || string.IsNullOrWhiteSpace(executionContext.Domain))
        {
            return new AskResult(
                false,
                string.Empty,
                "No hay dominio configurado para recuperacion de reglas.",
                false,
                AskFailureKind.GenerationError);
        }

        var normalizedDomain = executionContext.Domain.Trim();
        var profileKey = string.IsNullOrWhiteSpace(executionContext.SystemProfileKey)
            ? null
            : executionContext.SystemProfileKey.Trim();

        var allowedObjectNames = await GetAllowedObjectNamesAsync(
                normalizedDomain,
                ct);

        if (allowedObjectNames.Count == 0)
        {
            return new AskResult(
                false,
                string.Empty,
                $"No hay objetos SQL permitidos configurados para el dominio '{normalizedDomain}'.",
                false,
                AskFailureKind.GenerationError);
        }

        var patternMatch = await patternMatcher.MatchAsync(question, normalizedDomain, ct);
        var inferredIntentName = string.IsNullOrWhiteSpace(patternMatch.IntentName)
            ? null
            : patternMatch.IntentName;
        var inferredSemanticExpectation = HasSemanticExpectation(patternMatch)
            ? patternMatch
            : null;

        var verifiedExactMatch = await trainingStore.GetVerifiedExactMatchAsync(
            memoryDbPath,
            question,
            executionContext,
            ct);

        if (verifiedExactMatch is not null)
        {
            Console.WriteLine(
                $"[SqlRoute] Route=VerifiedExample Domain={normalizedDomain} Connection={executionContext.ConnectionName} Question=\"{question}\" ExampleId={verifiedExactMatch.Id}");
            var fastPathResult = await ProcessSqlCandidateAsync(
                                question,
                                memoryDbPath,
                                runtimeDbPath,
                                sqlServerConnString,
                                executionContext,
                                normalizedDomain,
                                verifiedExactMatch.Sql,
                                inferredIntentName,
                                allowedObjectNames,
                                profileKey,
                                semanticExpectation: inferredSemanticExpectation,
                                exampleIdToTouch: verifiedExactMatch.Id,
                                reviewReasonPrefix: "VerifiedExample",
                                ct: ct);
            return fastPathResult;
        }

        if (patternMatch.IsMatch)
        {
            Console.WriteLine(
                $"[SqlRoute] Route=Pattern Domain={normalizedDomain} Connection={executionContext.ConnectionName} Question=\"{question}\" PatternKey={patternMatch.PatternKey} Intent={patternMatch.IntentName} TopN={patternMatch.TopN} TimeScope={patternMatch.TimeScope}");
            var patternSql = templateSqlBuilder.BuildSql(patternMatch);

            if (string.IsNullOrWhiteSpace(patternSql))
            {
                return new AskResult(
                    false,
                    string.Empty,
                    "No se pudo construir SQL desde el patron detectado.",
                    false,
                    AskFailureKind.GenerationError);
            }

            return await ProcessSqlCandidateAsync(
                question,
                memoryDbPath,
                runtimeDbPath,
                sqlServerConnString,
                executionContext,
                normalizedDomain,
                patternSql,
                inferredIntentName,
                allowedObjectNames,
                profileKey,
                semanticExpectation: patternMatch,
                reviewReasonPrefix: "Pattern",
                ct: ct);
        }

        // Solo si no hubo pattern, caer al flujo LLM
        Console.WriteLine(
            $"[SqlRoute] Route=Llm Domain={normalizedDomain} Connection={executionContext.ConnectionName} Question=\"{question}\" Intent={inferredIntentName ?? string.Empty}");
        var context = await retriever.RetrieveAsync(
            memoryDbPath,
            question,
            executionContext,
            inferredIntentName,
            ct);

        var rules = await businessRuleStore.GetActiveRulesAsync(
            memoryDbPath,
            normalizedDomain,
            maxRules: 6,
            ct);

        var semanticHints = await semanticHintStore.GetActiveHintsAsync(
            memoryDbPath,
            normalizedDomain,
            maxHints: 8,
            ct);

        var prompt = await BuildPromptAsync(question, context, rules, semanticHints, allowedObjectNames, profileKey, null, ct);
        Console.WriteLine($"[SqlPrompt][Initial][Domain={normalizedDomain}][Question={question}]{Environment.NewLine}{prompt}");

        var rawSql = await llmClient.GenerateSqlAsync(prompt, ct);
        var cleanSql = CleanLlmOutput(rawSql);

        if (string.IsNullOrWhiteSpace(cleanSql))
        {
            return new AskResult(
                false,
                string.Empty,
                "El modelo no devolvio un SQL utilizable.",
                false,
                AskFailureKind.GenerationError);
        }

        return await ProcessSqlCandidateAsync(
                    question,
                    memoryDbPath,
                    runtimeDbPath,
                    sqlServerConnString,
                    executionContext,
                    normalizedDomain,
                    cleanSql,
                    inferredIntentName,
                    allowedObjectNames,
                    profileKey,
                    semanticExpectation: inferredSemanticExpectation,
                    existingContext: context,
                    existingRules: rules,
                    existingSemanticHints: semanticHints,
                    reviewReasonPrefix: "Llm",
                    ct: ct);
    }

    private async Task<AskResult> ProcessSqlCandidateAsync(
    string question,
    string memoryDbPath,
    string runtimeDbPath,
    string sqlServerConnString,
    AskExecutionContext executionContext,
    string normalizedDomain,
    string candidateSql,
    string? intentName,
    IReadOnlyList<string> allowedObjectNames,
    string? profileKey,
    PatternMatchResult? semanticExpectation,
    CancellationToken ct, // ✅ ahora va antes
    RetrievalContext? existingContext = null,
    IReadOnlyList<BusinessRule>? existingRules = null,
    IReadOnlyList<SemanticHint>? existingSemanticHints = null,
    long? exampleIdToTouch = null,
    string reviewReasonPrefix = "Sql")
    {
        var initialSql = CleanLlmOutput(candidateSql);
        if (string.IsNullOrWhiteSpace(initialSql))
        {
            return new AskResult(
                false,
                string.Empty,
                "El SQL candidato esta vacio o no es utilizable.",
                false,
                AskFailureKind.GenerationError);
        }

        var firstAttempt = await ValidateSqlCandidateAsync(
            initialSql,
            question,
            sqlServerConnString,
            normalizedDomain,
            semanticExpectation,
            ct);

        if (firstAttempt.Success)
        {
            if (exampleIdToTouch.HasValue)
            {
                await trainingStore.TouchExampleAsync(memoryDbPath, exampleIdToTouch.Value, ct);
            }
            return new AskResult(
                true,
                initialSql,
                null,
                firstAttempt.PassedDryRun,
                AskFailureKind.None);
        }

        if (firstAttempt.FailureKind is AskFailureKind.ValidationError or AskFailureKind.DryRunError)
        {
            var retryContext = existingContext ?? await retriever.RetrieveAsync(
                memoryDbPath,
                question,
                executionContext,
                intentName,
                ct);

            var retryRules = existingRules ?? await businessRuleStore.GetActiveRulesAsync(
                memoryDbPath,
                normalizedDomain,
                maxRules: 6,
                ct);

            var retrySemanticHints = existingSemanticHints ?? await semanticHintStore.GetActiveHintsAsync(
                memoryDbPath,
                normalizedDomain,
                maxHints: 8,
                ct);

            var correctedSql = await TrySelfCorrectSqlAsync(
                question,
                initialSql,
                firstAttempt.Error ?? "Fallo desconocido.",
                retryContext,
                retryRules,
                retrySemanticHints,
                allowedObjectNames,
                profileKey,
                ct);

            if (!string.IsNullOrWhiteSpace(correctedSql))
            {
                var normalizedCorrectedSql = CleanLlmOutput(correctedSql);
                if (!string.IsNullOrWhiteSpace(normalizedCorrectedSql))
                {
                    var secondAttempt = await ValidateSqlCandidateAsync(
                        normalizedCorrectedSql,
                        question,
                        sqlServerConnString,
                        normalizedDomain,
                        semanticExpectation,
                        ct);

                    if (secondAttempt.Success)
                    {
                        if (exampleIdToTouch.HasValue)
                        {
                            await trainingStore.TouchExampleAsync(memoryDbPath, exampleIdToTouch.Value, ct);
                        }

                        return new AskResult(
                            true,
                            normalizedCorrectedSql,
                            null,
                            secondAttempt.PassedDryRun,
                            AskFailureKind.None);
                    }

                    var retryReviewId = await TryEnqueueReviewAsync(
                        runtimeDbPath,
                        question,
                        normalizedCorrectedSql,
                        secondAttempt.Error,
                        $"{reviewReasonPrefix}RequiresReview",
                        ct);

                    return new AskResult(
                        false,
                        normalizedCorrectedSql,
                        $"No pude corregir el SQL automaticamente. {secondAttempt.Error}",
                        false,
                        secondAttempt.FailureKind,
                        retryReviewId);
                }
            }
        }

        var reviewId = await TryEnqueueReviewAsync(
            runtimeDbPath,
            question,
            initialSql,
            firstAttempt.Error,
            $"{reviewReasonPrefix}RequiresReview",
            ct);

        return new AskResult(
            false,
            initialSql,
            $"No pude corregir el SQL automaticamente. {firstAttempt.Error}",
            false,
            firstAttempt.FailureKind,
            reviewId);
    }

    public async Task<AskResult> PredictAsync(string question, AskExecutionContext executionContext, CancellationToken ct = default)
    {
        var intent = await predictionRouter.ParseAsync(question, ct);

        if (!intent.IsPredictionRequest || string.IsNullOrWhiteSpace(intent.EntityName))
        {
            return new AskResult(
                false,
                string.Empty,
                "No se identificó una entidad concreta para el pronóstico. Especifica un N/P, producto, prensa, cliente u otra clave de serie en la pregunta.",
                false,
                AskFailureKind.GenerationError);
        }

        intent = await forecaster.PredictAsync(intent, executionContext, ct);
        var humanizedText = await humanizer.HumanizeAsync(intent, ct);

        var predictionJson = JsonSerializer.Serialize(new
        {
            type = "prediction",
            explanation = humanizedText,
            data = new
            {
                EntityName = intent.EntityName,
                PredictedValue = intent.PredictedValue,
                HistoryShiftsUsed = intent.HistoryShiftsUsed,
                ForecastPeriodLabel = intent.ForecastPeriodLabel
            }
        });

        return new AskResult(
            true,
            humanizedText,
            null,
            true,
            AskFailureKind.None,
            null,
            predictionJson);
    }

    private async Task<IReadOnlyList<string>> GetAllowedObjectNamesAsync(
    string domain,
    CancellationToken ct)
    {
        var allowedObjects = await allowedObjectStore.GetActiveObjectsAsync(
            domain,
            ct);

        return allowedObjects
            .Where(x => x != null && !string.IsNullOrWhiteSpace(x.ObjectName))
            .Select(x =>
                string.IsNullOrWhiteSpace(x.SchemaName)
                    ? x.ObjectName.Trim()
                    : $"{x.SchemaName.Trim()}.{x.ObjectName.Trim()}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<long?> TryEnqueueReviewAsync(
        string runtimeDbPath,
        string question,
        string generatedSql,
        string? errorMessage,
        string reason,
        CancellationToken ct)
    {
        try
        {
            return await reviewStore.EnqueueAsync(
                runtimeDbPath,
                question,
                generatedSql,
                errorMessage,
                reason,
                ct);
        }
        catch
        {
            return null;
        }
    }

    private async Task<string> BuildPromptAsync(
        string question,
        RetrievalContext context,
        IReadOnlyList<BusinessRule> rules,
        IReadOnlyList<SemanticHint> semanticHints,
        IReadOnlyList<string> allowedObjectNames,
        string? profileKey,
        string? retryInstructions,
        CancellationToken ct)
    {
        var maxPromptChars = await GetPromptIntAsync("MaxPromptChars", 9000, profileKey, ct);
        var maxRulesChars = await GetPromptIntAsync("MaxRulesChars", 1800, profileKey, ct);
        var maxSemanticHintsChars = await GetPromptIntAsync("MaxSemanticHintsChars", 1400, profileKey, ct);
        var maxSchemasChars = await GetPromptIntAsync("MaxSchemasChars", 1800, profileKey, ct);
        var maxExamplesChars = await GetPromptIntAsync("MaxExamplesChars", 4200, profileKey, ct);
        var maxRules = await GetPromptIntAsync("MaxRules", 6, profileKey, ct);
        var maxSemanticHints = await GetPromptIntAsync("MaxSemanticHints", 8, profileKey, ct);
        var maxSchemas = await GetPromptIntAsync("MaxSchemas", 3, profileKey, ct);
        var maxExamples = await GetPromptIntAsync("MaxExamples", 2, profileKey, ct);
        var systemPersona = await GetPromptTextAsync("SystemPersona", "Eres un desarrollador experto en T-SQL para SQL Server.", profileKey, ct);
        var taskInstruction = await GetPromptTextAsync("TaskInstruction", "Tu tarea es generar SOLO codigo SQL valido para SQL Server.", profileKey, ct);
        var contextInstruction = await GetPromptTextAsync("ContextInstruction", "Debes basarte estrictamente en los objetos SQL permitidos, reglas, esquemas y ejemplos proporcionados.", profileKey, ct);
        var sqlSyntaxRules = await GetPromptTextAsync(
            "SqlSyntaxRules",
            """
            1. ESTA ESTRICTAMENTE PROHIBIDO USAR 'LIMIT'. Para limitar resultados en SQL Server, usa SIEMPRE 'SELECT TOP (N)'.
            2. Usa EXACTAMENTE los nombres de columnas que aparezcan en los esquemas recuperados y ejemplos validos.
            3. NUNCA compares un valor de texto contra una columna ID.
            4. Si necesitas cruzar objetos SQL permitidos, prefiere joins por IDs y OperationDate.
            5. Devuelve SOLO el SQL, sin comentarios y sin bloques markdown.
            """,
            profileKey,
            ct);
        var timeInterpretationRules = await GetPromptTextAsync(
            "TimeInterpretationRules",
            """
            - Hoy: CAST(OperationDate AS date) = CAST(GETDATE() AS date)
            - Ayer: CAST(OperationDate AS date) = DATEADD(DAY, -1, CAST(GETDATE() AS date))
            - Mes actual: YearMonth = CONVERT(char(7), GETDATE(), 120)
            - Semana actual: YearNumber = YEAR(GETDATE()) AND WeekOfYear = DATEPART(ISO_WEEK, GETDATE())
            - Cuando el usuario diga 'turno actual' o 'del turno', filtra explicitamente por un unico ShiftId calculado como el mas reciente del dia dentro de la vista consultada.
            """,
            profileKey,
            ct);
        var rulesHeader = await GetPromptTextAsync("BusinessRulesHeader", "REGLAS DE NEGOCIO IMPORTANTES:", profileKey, ct);
        var semanticHintsHeader = await GetPromptTextAsync("SemanticHintsHeader", "PISTAS SEMANTICAS DEL DOMINIO:", profileKey, ct);
        var allowedObjectsHeader = await GetPromptTextAsync("AllowedObjectsHeader", "OBJETOS SQL PERMITIDOS:", profileKey, ct);
        var schemasHeader = await GetPromptTextAsync("SchemasHeader", "ESQUEMAS RELEVANTES RECUPERADOS:", profileKey, ct);
        var examplesHeader = await GetPromptTextAsync("ExamplesHeader", "EJEMPLOS RELEVANTES:", profileKey, ct);
        var questionHeader = await GetPromptTextAsync("QuestionHeader", "Pregunta actual:", profileKey, ct);

        var normalizedAllowedObjects = allowedObjectNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(NormalizeObjectName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var allowedObjectsText = string.Join(
            "\n",
            allowedObjectNames.Select(x => $"- {x}"));

        var activeRules = (rules ?? Array.Empty<BusinessRule>())
            .Where(r => r != null && !string.IsNullOrWhiteSpace(r.RuleText) && r.IsActive)
            .OrderBy(r => r.Priority)
            .Take(maxRules)
            .Select(r => $"- {r.RuleText.Trim()}")
            .ToList();

        var rulesText = activeRules.Any()
            ? TrimToMax(string.Join("\n", activeRules), maxRulesChars)
            : string.Empty;

        var semanticHintLines = (semanticHints ?? Array.Empty<SemanticHint>())
            .Where(h => h != null && h.IsActive && !string.IsNullOrWhiteSpace(h.HintText))
            .Where(h => ShouldIncludeSemanticHint(h, normalizedAllowedObjects))
            .OrderBy(h => h.Priority)
            .Take(maxSemanticHints)
            .Select(FormatSemanticHint)
            .ToList();

        var semanticHintsText = semanticHintLines.Any()
            ? TrimToMax(string.Join("\n", semanticHintLines), maxSemanticHintsChars)
            : string.Empty;

        var schemaLines = (context?.SchemaDocs ?? Enumerable.Empty<RetrievedSchemaDoc>())
            .Where(s => s?.Doc != null)
            .Where(s => normalizedAllowedObjects.Contains(NormalizeObjectName(s.Doc!.Table)))
            .Select(s => FormatSchemaDocForPrompt(s.Doc!))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct()
            .Take(maxSchemas)
            .ToList();

        var schemasText = schemaLines.Any()
            ? TrimToMax(string.Join("\n", schemaLines), maxSchemasChars)
            : string.Empty;

        var exampleLines = (context?.Examples ?? Enumerable.Empty<RetrievedExample>())
            .Where(e => e?.Example != null)
            .Where(e =>
                !string.IsNullOrWhiteSpace(e.Example.Question) &&
                !string.IsNullOrWhiteSpace(e.Example.Sql) &&
                MentionsAllowedObject(e.Example.Sql, normalizedAllowedObjects))
            .Take(maxExamples)
            .Select(e => $"Pregunta: {e.Example!.Question.Trim()}\nSQL:\n{e.Example.Sql.Trim()}")
            .ToList();

        var examplesText = exampleLines.Any()
            ? TrimToMax(string.Join("\n\n", exampleLines), maxExamplesChars)
            : string.Empty;

        var sb = new StringBuilder(4096);

        sb.AppendLine("<|im_start|>system");
        sb.AppendLine(systemPersona);
        sb.AppendLine(taskInstruction);
        sb.AppendLine(contextInstruction);
        sb.AppendLine();
        sb.AppendLine("REGLAS CRITICAS DE SINTAXIS T-SQL:");
        sb.AppendLine(sqlSyntaxRules);
        sb.AppendLine();
        sb.AppendLine("VALIDACION ESTRICTA DE NOMBRES Y ESTRUCTURA:");
        sb.AppendLine("- Usa SOLO columnas y objetos que aparezcan textual y exactamente en ESQUEMAS RELEVANTES, PISTAS SEMANTICAS o EJEMPLOS RELEVANTES.");
        sb.AppendLine("- Si un nombre de columna no aparece de forma explicita en esos bloques, NO lo inventes.");
        sb.AppendLine("- Devuelve una sola sentencia SQL valida: un unico SELECT o WITH...SELECT.");
        sb.AppendLine("- Si la pregunta habla de scrap, prioriza la metrica real expuesta por el esquema recuperado; no inventes variantes como Quantity si el esquema usa Qty.");
        sb.AppendLine("- Si el usuario pregunta por una prensa, maquina o equipo, prefiere devolver el nombre visible (por ejemplo PressName) y usa el ID solo como apoyo o fallback, salvo que el usuario pida explicitamente el ID.");
        sb.AppendLine();
        sb.AppendLine("REGLAS DE TIEMPO E INTERPRETACION:");
        sb.AppendLine(timeInterpretationRules);
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(rulesText))
        {
            sb.AppendLine(rulesHeader);
            sb.AppendLine(rulesText);
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(semanticHintsText))
        {
            sb.AppendLine(semanticHintsHeader);
            sb.AppendLine(semanticHintsText);
            sb.AppendLine();
        }

        sb.AppendLine(allowedObjectsHeader);
        sb.AppendLine(allowedObjectsText);
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(schemasText))
        {
            sb.AppendLine(schemasHeader);
            sb.AppendLine(schemasText);
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(examplesText))
        {
            sb.AppendLine(examplesHeader);
            sb.AppendLine(examplesText);
            sb.AppendLine();
        }

        sb.AppendLine("<|im_end|>");
        sb.AppendLine("<|im_start|>user");
        sb.AppendLine(questionHeader);
        sb.AppendLine(question?.Trim() ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(retryInstructions))
        {
            sb.AppendLine();
            sb.AppendLine("CONTEXTO DE CORRECCION:");
            sb.AppendLine(retryInstructions.Trim());
        }
        sb.AppendLine("<|im_end|>");
        sb.AppendLine("<|im_start|>assistant");

        return TrimToMax(sb.ToString(), maxPromptChars);
    }

    private async Task<int> GetPromptIntAsync(string key, int fallback, string? profileKey, CancellationToken ct)
    {
        var value = await systemConfigProvider.GetIntAsync("Prompting", key, profileKey, ct);
        return value.HasValue && value.Value > 0 ? value.Value : fallback;
    }

    private async Task<string> GetPromptTextAsync(string key, string fallback, string? profileKey, CancellationToken ct)
    {
        var value = await systemConfigProvider.GetValueAsync("Prompting", key, profileKey, ct);
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string NormalizeObjectName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var cleaned = name.Trim()
            .Replace("[", string.Empty)
            .Replace("]", string.Empty);

        if (cleaned.StartsWith("dbo.", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned[4..];

        return cleaned.ToUpperInvariant();
    }

    private static bool MentionsAllowedObject(
        string sql,
        IReadOnlyCollection<string> normalizedAllowedObjects)
    {
        if (string.IsNullOrWhiteSpace(sql) || normalizedAllowedObjects.Count == 0)
            return false;

        var normalizedSql = sql.ToUpperInvariant()
            .Replace("[", string.Empty)
            .Replace("]", string.Empty);

        return normalizedAllowedObjects.Any(obj =>
            normalizedSql.Contains(obj, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ShouldIncludeSemanticHint(
        SemanticHint hint,
        IReadOnlySet<string> normalizedAllowedObjects)
    {
        if (hint is null)
            return false;

        if (string.IsNullOrWhiteSpace(hint.ObjectName))
            return true;

        return normalizedAllowedObjects.Contains(NormalizeObjectName(hint.ObjectName));
    }

    private static string FormatSemanticHint(SemanticHint hint)
    {
        var label = !string.IsNullOrWhiteSpace(hint.DisplayName)
            ? hint.DisplayName!.Trim()
            : hint.HintKey.Trim();

        var targetParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(hint.ObjectName))
            targetParts.Add(hint.ObjectName.Trim());
        if (!string.IsNullOrWhiteSpace(hint.ColumnName))
            targetParts.Add(hint.ColumnName.Trim());

        var target = targetParts.Count > 0
            ? $" | Target: {string.Join(".", targetParts)}"
            : string.Empty;

        var hintType = string.IsNullOrWhiteSpace(hint.HintType)
            ? "hint"
            : hint.HintType.Trim().ToLowerInvariant();

        return $"- [{hintType}] {label}: {hint.HintText.Trim()}{target}";
    }

    private static string FormatSchemaDocForPrompt(TableSchemaDoc doc)
    {
        if (doc is null)
            return string.Empty;

        var tableLabel = string.IsNullOrWhiteSpace(doc.Schema)
            ? doc.Table?.Trim() ?? string.Empty
            : $"{doc.Schema.Trim()}.{doc.Table.Trim()}";

        if (string.IsNullOrWhiteSpace(doc.Json))
            return doc.DocText?.Trim() ?? string.Empty;

        try
        {
            var schema = JsonSerializer.Deserialize<TableSchema>(doc.Json);
            if (schema is null)
                return doc.DocText?.Trim() ?? string.Empty;

            var columnLines = schema.Columns?
                .Where(c => c is not null && !string.IsNullOrWhiteSpace(c.Name))
                .Take(18)
                .Select(c =>
                {
                    var nullable = c.IsNullable ? "NULL" : "NOT NULL";
                    var typeSuffix =
                        c.MaxLength is > 0 ? $"({c.MaxLength})" :
                        c.Precision.HasValue && c.Scale.HasValue ? $"({c.Precision},{c.Scale})" :
                        string.Empty;
                    return $"- {c.Name}: {c.SqlType}{typeSuffix} {nullable}";
                })
                .ToList() ?? [];

            var pkLine = schema.PrimaryKeyColumns is { Count: > 0 }
                ? $"PK: {string.Join(", ", schema.PrimaryKeyColumns)}"
                : null;

            var fkLines = schema.ForeignKeys is { Count: > 0 }
                ? schema.ForeignKeys
                    .Take(4)
                    .Select(fk => $"- FK {fk.FromColumn} -> {fk.ToSchema}.{fk.ToTable}.{fk.ToColumn}")
                    .ToList()
                : [];

            var sb = new StringBuilder();
            sb.AppendLine($"Tabla: {tableLabel}");
            if (!string.IsNullOrWhiteSpace(schema.Description))
            {
                sb.AppendLine($"Descripcion: {schema.Description.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(pkLine))
            {
                sb.AppendLine(pkLine);
            }

            if (columnLines.Count > 0)
            {
                sb.AppendLine("Columnas:");
                foreach (var line in columnLines)
                    sb.AppendLine(line);
            }

            if (fkLines.Count > 0)
            {
                sb.AppendLine("Foreign keys:");
                foreach (var line in fkLines)
                    sb.AppendLine(line);
            }

            var text = sb.ToString().Trim();
            return string.IsNullOrWhiteSpace(text) ? (doc.DocText?.Trim() ?? string.Empty) : text;
        }
        catch
        {
            return doc.DocText?.Trim() ?? string.Empty;
        }
    }

    private static string TrimToMax(string text, int maxChars)
    {
        const string suffix = "\n...[contenido truncado por presupuesto de prompt]";

        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        if (text.Length <= maxChars)
            return text;

        if (maxChars <= suffix.Length)
            return text.Substring(0, maxChars);

        return text.Substring(0, maxChars - suffix.Length) + suffix;
    }

    private static string CleanLlmOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return string.Empty;

        var clean = output.Trim();

        clean = Regex.Replace(clean, @"```sql|```", string.Empty, RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"<\|im_end\|>.*$", string.Empty, RegexOptions.Singleline);

        var sqlMatch = Regex.Match(clean, @"(?is)\b(WITH|SELECT)\b[\s\S]*");
        if (sqlMatch.Success)
            clean = sqlMatch.Value;

        return clean.Trim();
    }

    private async Task<(bool Success, AskFailureKind FailureKind, string? Error, bool PassedDryRun)> ValidateSqlCandidateAsync(
        string sql,
        string question,
        string sqlServerConnString,
        string normalizedDomain,
        PatternMatchResult? semanticExpectation,
        CancellationToken ct)
    {
        var semanticError = ValidateSemanticAlignment(question, sql, semanticExpectation);
        if (!string.IsNullOrWhiteSpace(semanticError))
        {
            return (false, AskFailureKind.ValidationError, $"Validacion semantica fallida: {semanticError}", false);
        }

        if (!validator.TryValidate(sql, normalizedDomain, out var validationError))
        {
            return (false, AskFailureKind.ValidationError, $"Validacion fallida: {validationError}", false);
        }

        if (!settings.Security.DryRunEnabledByDefault)
        {
            return (true, AskFailureKind.None, null, false);
        }

        var (ok, error) = await dryRunner.DryRunAsync(sqlServerConnString, sql, ct);
        if (!ok)
        {
            return (false, AskFailureKind.DryRunError, $"Error de compilacion: {error}", false);
        }

        return (true, AskFailureKind.None, null, true);
    }

    private static string? ValidateSemanticAlignment(
        string question,
        string sql,
        PatternMatchResult? semanticExpectation)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return "La consulta candidata esta vacia.";
        }

        if (semanticExpectation is null)
        {
            return null;
        }

        var upperSql = sql.ToUpperInvariant();
        var upperQuestion = question?.ToUpperInvariant() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(semanticExpectation.DimensionValue))
        {
            if (!upperSql.Contains("WHERE", StringComparison.Ordinal))
            {
                return $"La pregunta pide una entidad concreta ({semanticExpectation.DimensionValue}) pero el SQL no incluye WHERE.";
            }

            var normalizedDimensionValue = semanticExpectation.DimensionValue.Trim().ToUpperInvariant();
            if (!HasEntitySpecificFilter(upperSql, normalizedDimensionValue))
            {
                return $"La pregunta pide la entidad '{semanticExpectation.DimensionValue}' pero el SQL no refleja ese filtro.";
            }

            if (semanticExpectation.Dimension != PatternDimension.Unknown &&
                !HasDimensionFilterClause(upperSql, semanticExpectation.Dimension))
            {
                return $"La pregunta pide una sola entidad ({semanticExpectation.DimensionValue}) pero el SQL parece responder con agregado global sin filtro de dimension.";
            }
        }

        if (semanticExpectation.TimeScope == PatternTimeScope.CurrentShift ||
            upperQuestion.Contains("TURNO ACTUAL", StringComparison.Ordinal))
        {
            if (!HasCurrentShiftFilter(upperSql))
            {
                return "La pregunta pide turno actual pero el SQL no incluye un filtro temporal equivalente por fecha actual y ShiftId.";
            }
        }

        if (semanticExpectation.TopN > 0)
        {
            if (!Regex.IsMatch(upperSql, @"\bSELECT\s+TOP\s*\(", RegexOptions.CultureInvariant) &&
                !Regex.IsMatch(upperSql, @"\bSELECT\s+TOP\s+\d+", RegexOptions.CultureInvariant))
            {
                return $"La pregunta pide top {semanticExpectation.TopN} pero el SQL no incluye TOP.";
            }

            if (semanticExpectation.Dimension != PatternDimension.Unknown &&
                !upperSql.Contains("GROUP BY", StringComparison.Ordinal))
            {
                return $"La pregunta pide top {semanticExpectation.TopN} por dimension pero el SQL no incluye GROUP BY.";
            }

            if (!upperSql.Contains("ORDER BY", StringComparison.Ordinal))
            {
                return $"La pregunta pide top {semanticExpectation.TopN} pero el SQL no incluye ORDER BY.";
            }
        }

        return null;
    }

    private static bool HasSemanticExpectation(PatternMatchResult? match)
    {
        if (match is null)
        {
            return false;
        }

        return match.Metric != PatternMetric.Unknown
            || match.Dimension != PatternDimension.Unknown
            || !string.IsNullOrWhiteSpace(match.DimensionValue)
            || match.TimeScope != PatternTimeScope.Unknown
            || match.TopN > 0;
    }

    private static bool HasEntitySpecificFilter(string upperSql, string normalizedDimensionValue)
    {
        return !string.IsNullOrWhiteSpace(normalizedDimensionValue) &&
               upperSql.Contains(normalizedDimensionValue, StringComparison.Ordinal);
    }

    private static bool HasDimensionFilterClause(string upperSql, PatternDimension dimension)
    {
        var dimensionSignals = dimension switch
        {
            PatternDimension.Press => new[] { "PRESSNAME", "PRESSID" },
            PatternDimension.PartNumber => new[] { "PARTNUMBER" },
            PatternDimension.Mold => new[] { "MOLDNAME", "MOLDID" },
            PatternDimension.Failure => new[] { "FAILURENAME", "FAILUREID" },
            PatternDimension.Department => new[] { "DEPARTMENTNAME", "DEPARTMENTID" },
            _ => Array.Empty<string>()
        };

        if (dimensionSignals.Length == 0)
        {
            return true;
        }

        var whereIndex = upperSql.IndexOf("WHERE", StringComparison.Ordinal);
        if (whereIndex < 0)
        {
            return false;
        }

        var tail = upperSql[whereIndex..];
        return dimensionSignals.Any(signal => tail.Contains(signal, StringComparison.Ordinal));
    }

    private static bool HasCurrentShiftFilter(string upperSql)
    {
        var hasShiftId = upperSql.Contains("SHIFTID", StringComparison.Ordinal);
        var hasCurrentDateReference =
            upperSql.Contains("OPERATIONDATE", StringComparison.Ordinal) &&
            (upperSql.Contains("GETDATE()", StringComparison.Ordinal) ||
             upperSql.Contains("CAST(GETDATE() AS DATE)", StringComparison.Ordinal) ||
             upperSql.Contains("CONVERT(DATE, GETDATE())", StringComparison.Ordinal));

        return hasShiftId && hasCurrentDateReference;
    }

    private async Task<string> TrySelfCorrectSqlAsync(
        string question,
        string failedSql,
        string exactError,
        RetrievalContext context,
        IReadOnlyList<BusinessRule> rules,
        IReadOnlyList<SemanticHint> semanticHints,
        IReadOnlyList<string> allowedObjectNames,
        string? profileKey,
        CancellationToken ct)
    {
        var invalidIdentifiers = ExtractInvalidIdentifiers(exactError);
        var invalidIdentifiersBlock = invalidIdentifiers.Count == 0
            ? string.Empty
            : $"""

            Identificadores invalidos detectados en el error:
            {string.Join("\n", invalidIdentifiers.Select(x => $"- {x}"))}
            """;

        var retryInstructions =
            $"""
            La propuesta anterior fallo y debes corregirla en un solo reintento.
            Pregunta original: {question.Trim()}
            SQL fallido:
            {failedSql.Trim()}

            Error exacto:
            {exactError.Trim()}
            {invalidIdentifiersBlock}

            Reglas obligatorias para corregir:
            - conserva la intencion original de la pregunta
            - usa solo objetos SQL permitidos
            - no inventes tablas, vistas ni columnas
            - no vuelvas a usar identificadores marcados como invalidos en el error
            - devuelve una sola sentencia SQL valida
            - corrige el SQL usando el error exacto de validacion o compilacion
            - devuelve SOLO SQL valido para SQL Server
            """;

        var retryPrompt = await BuildPromptAsync(
            question,
            context,
            rules,
            semanticHints,
            allowedObjectNames,
            profileKey,
            retryInstructions,
            ct);
        Console.WriteLine($"[SqlPrompt][Retry][Question={question}]{Environment.NewLine}{retryPrompt}");

        return await llmClient.GenerateSqlAsync(retryPrompt, ct);
    }

    private static IReadOnlyList<string> ExtractInvalidIdentifiers(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return Array.Empty<string>();

        var matches = Regex.Matches(
            error,
            @"Invalid\s+(?:column|object)\s+name\s+'(?<name>[^']+)'",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return matches
            .Select(match => match.Groups["name"].Value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
