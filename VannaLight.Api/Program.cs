using Dapper;
using LLama.Native;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using VannaLight.Api.Hubs;
using VannaLight.Api.Services;
using VannaLight.Api.Services.Docs;
using VannaLight.Api.Services.Predictions;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;
using VannaLight.Core.Settings;
using VannaLight.Core.UseCases;
using VannaLight.Infrastructure.AI;
using VannaLight.Infrastructure.Configuration;
using VannaLight.Infrastructure.Data;
using VannaLight.Infrastructure.Retrieval;
using VannaLight.Infrastructure.Security;
using VannaLight.Infrastructure.SqlServer;

// =========================================================
// CONFIGURACION NATIVA DE LLamaSharp
// Debe ir antes de cualquier uso del motor
// =========================================================
NativeLibraryConfig.All
    .WithLogCallback((level, message) =>
    {
        Console.WriteLine($"[LLamaSharp][{level}] {message}");
    })
    .WithCuda()
    .WithAutoFallback(false)
    .SkipCheck(true);

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Carga de secretos en desarrollo
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

// ---------------------------------------------------------
// 1. GESTION DE RUTAS Y DIRECTORIOS (ContentRoot)
// ---------------------------------------------------------
var localAppDataRoot = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "VannaLight",
    builder.Environment.EnvironmentName,
    "Data");
var legacyDataRoot = Path.Combine(builder.Environment.ContentRootPath, "Data");

var sqlitePath = ResolveAppPath(
    builder.Configuration["Paths:Sqlite"],
    Path.Combine(localAppDataRoot, "vanna_memory.db"),
    builder.Environment.ContentRootPath);

var runtimePath = ResolveAppPath(
    builder.Configuration["Paths:RuntimeDb"],
    Path.Combine(localAppDataRoot, "vanna_runtime.db"),
    builder.Environment.ContentRootPath);

var dataProtectionKeysPath = ResolveAppPath(
    builder.Configuration["Paths:DataProtectionKeys"],
    Path.Combine(localAppDataRoot, "dpkeys"),
    builder.Environment.ContentRootPath);

var modelRelOrAbs = builder.Configuration["Paths:Model"] ?? @"C:\Modelos\qwen2.5-coder-7b-instruct-q4_k_m.gguf";
var modelPath = Path.IsPathRooted(modelRelOrAbs)
    ? modelRelOrAbs
    : Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, modelRelOrAbs));

Directory.CreateDirectory(Path.GetDirectoryName(sqlitePath)!);
Directory.CreateDirectory(Path.GetDirectoryName(runtimePath)!);
Directory.CreateDirectory(dataProtectionKeysPath);
MigrateLegacyLocalState(legacyDataRoot, sqlitePath, runtimePath, dataProtectionKeysPath);

// ---------------------------------------------------------
// 2. CONFIGURACION DE ARRANQUE
// ---------------------------------------------------------
var environmentName = builder.Configuration["SystemStartup:EnvironmentName"] ?? builder.Environment.EnvironmentName;
var defaultSystemProfile = builder.Configuration["SystemStartup:DefaultSystemProfile"] ?? "default";

// ---------------------------------------------------------
// 3. REGISTRO DE CONFIGURACIONES BASE
// ---------------------------------------------------------
var sqliteOptions = new SqliteOptions(sqlitePath);
var runtimeOptions = new RuntimeDbOptions(runtimePath);
var kpiViewOptions = builder.Configuration.GetSection("KpiViews").Get<KpiViewOptions>() ?? new KpiViewOptions();

// Registro directo de options concretas
builder.Services.AddSingleton(sqliteOptions);
builder.Services.AddSingleton(runtimeOptions);
builder.Services.AddSingleton(kpiViewOptions);
builder.Services.AddSingleton<IOptions<SqliteOptions>>(Options.Create(sqliteOptions));
builder.Services.AddSingleton<IOptions<RuntimeDbOptions>>(Options.Create(runtimeOptions));
builder.Services.AddSingleton<IOptions<KpiViewOptions>>(Options.Create(kpiViewOptions));

// AppSettings actual: se mantiene por compatibilidad hasta mover LlmClient
var settings = AppSettingsFactory.Create(RuntimeProfile.ALTO, modelPath);
builder.Services.AddSingleton(settings);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("VannaLight");

// ---------------------------------------------------------
// 4. NUEVA CAPA DE CONFIGURACION OPERATIVA
// ---------------------------------------------------------
builder.Services.AddSingleton<ISystemConfigStore>(_ => new SqliteSystemConfigStore(sqlitePath));
builder.Services.AddSingleton<IConnectionProfileStore>(_ => new SqliteConnectionProfileStore(sqlitePath));
builder.Services.AddSingleton<IAppSecretStore>(_ => new SqliteAppSecretStore(sqlitePath));
builder.Services.AddSingleton<ITenantStore>(_ => new SqliteTenantStore(sqlitePath));
builder.Services.AddSingleton<ITenantDomainStore>(_ => new SqliteTenantDomainStore(sqlitePath));
builder.Services.AddSingleton<IPredictionProfileStore, SqlitePredictionProfileStore>();
builder.Services.AddSingleton<ISystemConfigProvider, SystemConfigProvider>();
builder.Services.AddSingleton<ISecretResolver, CompositeSecretResolver>();
builder.Services.AddSingleton<IOperationalConnectionResolver, OperationalConnectionResolver>();
builder.Services.AddSingleton<IExecutionContextResolver, ExecutionContextResolver>();

// ---------------------------------------------------------
// 5. IA Y CONFIGURACION DE INFERENCIA
// ---------------------------------------------------------
builder.Services.AddSingleton<ILlmRuntimeProfileProvider, SqliteLlmProfileProvider>();
builder.Services.AddSingleton<ILlmProfileStore, SqliteLlmProfileStore>();
builder.Services.AddSingleton<ILlmClient, LlmClient>();

// ---------------------------------------------------------
// 6. SERVICIOS BASE E INFRAESTRUCTURA
// ---------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
builder.Services.AddMemoryCache();

// Casos de uso
builder.Services.AddTransient<AskUseCase>();
builder.Services.AddTransient<TrainExampleUseCase>();
builder.Services.AddSingleton<IngestUseCase>();

// Stores y repositorios
builder.Services.AddSingleton<ISchemaStore, SqliteSchemaStore>();
builder.Services.AddSingleton<ITrainingStore, SqliteTrainingStore>();
builder.Services.AddSingleton<IReviewStore, SqliteReviewStore>();
builder.Services.AddSingleton<IDocChunkRepository, SqliteDocChunkRepository>();
builder.Services.AddTransient<IJobStore, SqliteJobStore>();
builder.Services.AddSingleton<IBusinessRuleStore, SqliteBusinessRuleStore>();
builder.Services.AddSingleton<ISemanticHintStore, SqliteSemanticHintStore>();
builder.Services.AddSingleton<IAllowedObjectStore, SqliteAllowedObjectStore>();
builder.Services.AddSingleton<IQueryPatternStore, SqliteQueryPatternStore>();
builder.Services.AddSingleton<IQueryPatternTermStore, SqliteQueryPatternTermStore>();
builder.Services.AddSingleton<ISqlCacheService, SqlCacheService>();

// Ingesta de esquema
builder.Services.AddSingleton<ISchemaIngestor, SqlServerSchemaIngestor>();

// Pattern-first
builder.Services.AddSingleton<ITemplateSqlBuilder, TemplateSqlBuilder>();
builder.Services.AddSingleton<IPatternMatcherService, PatternMatcherService>();

// Retrieval, seguridad y ejecucion
builder.Services.AddSingleton<IRetriever, LocalRetriever>();
builder.Services.AddSingleton<ISqlValidator, StaticSqlValidator>();
builder.Services.AddSingleton<ISqlDryRunner, SqlServerDryRunner>();

// ---------------------------------------------------------
// 7. DOCS (RAG) Y MACHINE LEARNING
// ---------------------------------------------------------
builder.Services.AddSingleton<DocumentIngestor>();
builder.Services.AddSingleton<WiDocIngestor>();
builder.Services.AddSingleton<IDocsIntentRouter, DocsIntentRouterLlm>();
builder.Services.AddSingleton<IDocChunkScorer, DocChunkScorer>();
builder.Services.AddSingleton<IDocAnswerComposer, DocAnswerComposer>();
builder.Services.AddSingleton<IDocsAnswerService, DocsAnswerService>();

builder.Services.AddSingleton<IMlTrainingProfileProvider, MlTrainingProfileProvider>();
builder.Services.AddSingleton<IndustrialDomainPackAdapter>();
builder.Services.AddSingleton<NorthwindSalesDomainPackAdapter>();
builder.Services.AddSingleton<IDomainPackProvider, CompositeDomainPackProvider>();
builder.Services.AddSingleton<IPredictionIntentRouter, PredictionIntentRouterLlm>();
builder.Services.AddSingleton<IForecastingService, ForecastingService>();
builder.Services.AddSingleton<IPredictionAnswerService, PredictionAnswerService>();

// ---------------------------------------------------------
// 8. WORKER DE INFERENCIA
// ---------------------------------------------------------
builder.Services.AddSingleton<IAskRequestQueue, AskRequestQueue>();
builder.Services.AddHostedService<InferenceWorker>();

var app = builder.Build();

// ---------------------------------------------------------
// 9. INICIALIZACION DE BASES DE DATOS LOCALES
// ---------------------------------------------------------
await EnsureSystemConfigDatabaseSetupAsync(
    sqlitePath,
    builder.Configuration,
    environmentName,
    defaultSystemProfile);
await EnsureMemoryFeatureDatabaseSetupAsync(sqlitePath);
await EnsureSeededConnectionProfilesAndContextMappingsAsync(
    sqlitePath,
    builder.Configuration,
    environmentName,
    defaultSystemProfile);
await EnsureRuntimeDatabaseSetupAsync(app.Services);
await EnsureCorePilotQueryPatternSeedsAsync(app.Services, builder.Configuration);
await EnsureQueryPatternTimeScopeSeedsAsync(app.Services);
await EnsureCorePilotSemanticHintSeedsAsync(app.Services, builder.Configuration, sqlitePath);
await ReportPilotContextMemoryHealthAsync(sqlitePath, builder.Configuration);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
    {
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
        }
    }
    catch (TaskCanceledException) when (context.RequestAborted.IsCancellationRequested)
    {
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
        }
    }
});

app.UseStaticFiles();
app.UseAuthorization();

app.MapGet("/health", (HttpContext httpContext) =>
{
    var request = httpContext.Request;

    return Results.Ok(new
    {
        status = "ok",
        service = "VannaLight.Api",
        utc = DateTime.UtcNow,
        scheme = request.Scheme,
        host = request.Host.Value,
        pathBase = request.PathBase.Value ?? string.Empty
    });
});

app.MapControllers();
app.MapHub<AssistantHub>("/hub/assistant");

app.Run();

static string ResolveAppPath(string? configuredPath, string fallbackAbsolutePath, string contentRootPath)
{
    if (string.IsNullOrWhiteSpace(configuredPath))
        return fallbackAbsolutePath;

    var expanded = Environment.ExpandEnvironmentVariables(configuredPath.Trim());
    if (Path.IsPathRooted(expanded))
        return Path.GetFullPath(expanded);

    return Path.GetFullPath(Path.Combine(contentRootPath, expanded));
}

static void MigrateLegacyLocalState(
    string legacyDataRoot,
    string sqlitePath,
    string runtimePath,
    string dataProtectionKeysPath)
{
    if (!Directory.Exists(legacyDataRoot))
        return;

    TryCopyIfMissing(
        Path.Combine(legacyDataRoot, "vanna_memory.db"),
        sqlitePath,
        "vanna_memory.db");

    TryCopyIfMissing(
        Path.Combine(legacyDataRoot, "vanna_runtime.db"),
        runtimePath,
        "vanna_runtime.db");

    var legacyKeysDir = Path.Combine(legacyDataRoot, "dpkeys");
    if (!Directory.Exists(legacyKeysDir))
        return;

    Directory.CreateDirectory(dataProtectionKeysPath);
    foreach (var file in Directory.EnumerateFiles(legacyKeysDir))
    {
        var targetFile = Path.Combine(dataProtectionKeysPath, Path.GetFileName(file));
        if (File.Exists(targetFile))
            continue;

        File.Copy(file, targetFile);
        Console.WriteLine($"[LocalState] Copiada llave de proteccion local a '{targetFile}'.");
    }
}

static void TryCopyIfMissing(string sourceFile, string targetFile, string label)
{
    if (!File.Exists(sourceFile) || File.Exists(targetFile))
        return;

    Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
    File.Copy(sourceFile, targetFile);
    Console.WriteLine($"[LocalState] Migrado {label} desde la carpeta legacy del repo a '{targetFile}'.");
}

static async Task EnsureContextManagementSchemaAsync(SqliteConnection connection)
{
    var connectionProfileColumns = (await connection.QueryAsync<(int Cid, string Name, string Type, int NotNull, string? DefaultValue, int Pk)>("PRAGMA table_info(ConnectionProfiles);"))
        .Select(x => x.Name)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    if (!connectionProfileColumns.Contains("ManagementMode"))
    {
        await connection.ExecuteAsync("ALTER TABLE ConnectionProfiles ADD COLUMN ManagementMode TEXT NOT NULL DEFAULT 'UserManaged';");
    }

    var tenantColumns = (await connection.QueryAsync<(int Cid, string Name, string Type, int NotNull, string? DefaultValue, int Pk)>("PRAGMA table_info(Tenants);"))
        .Select(x => x.Name)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    if (!tenantColumns.Contains("ManagementMode"))
    {
        await connection.ExecuteAsync("ALTER TABLE Tenants ADD COLUMN ManagementMode TEXT NOT NULL DEFAULT 'UserManaged';");
    }

    var tenantDomainColumns = (await connection.QueryAsync<(int Cid, string Name, string Type, int NotNull, string? DefaultValue, int Pk)>("PRAGMA table_info(TenantDomains);"))
        .Select(x => x.Name)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    if (!tenantDomainColumns.Contains("ManagementMode"))
    {
        await connection.ExecuteAsync("ALTER TABLE TenantDomains ADD COLUMN ManagementMode TEXT NOT NULL DEFAULT 'UserManaged';");
    }
}

// =========================================================
// PREPARACION DE SYSTEM CONFIG (SQLite principal)
// =========================================================
static async Task EnsureSystemConfigDatabaseSetupAsync(
    string sqlitePath,
    IConfiguration configuration,
    string environmentName,
    string defaultSystemProfile)
{
    using var connection = new SqliteConnection($"Data Source={sqlitePath};");
    await connection.OpenAsync();

    const string sql = @"
        CREATE TABLE IF NOT EXISTS SystemConfigProfiles (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            EnvironmentName TEXT NOT NULL,
            ProfileKey TEXT NOT NULL,
            DisplayName TEXT NOT NULL,
            Description TEXT NULL,
            IsActive INTEGER NOT NULL DEFAULT 0,
            IsReadOnly INTEGER NOT NULL DEFAULT 0,
            CreatedUtc TEXT NOT NULL,
            UpdatedUtc TEXT NOT NULL,
            UNIQUE(EnvironmentName, ProfileKey)
        );

        CREATE TABLE IF NOT EXISTS SystemConfigEntries (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            ProfileId INTEGER NOT NULL,
            Section TEXT NOT NULL,
            [Key] TEXT NOT NULL,
            Value TEXT NULL,
            ValueType TEXT NOT NULL DEFAULT 'string',
            IsSecret INTEGER NOT NULL DEFAULT 0,
            SecretRef TEXT NULL,
            IsEditableInUi INTEGER NOT NULL DEFAULT 1,
            ValidationRule TEXT NULL,
            Description TEXT NULL,
            CreatedUtc TEXT NOT NULL,
            UpdatedUtc TEXT NOT NULL,
            FOREIGN KEY(ProfileId) REFERENCES SystemConfigProfiles(Id),
            UNIQUE(ProfileId, Section, [Key])
        );

        CREATE TABLE IF NOT EXISTS ConnectionProfiles (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            EnvironmentName TEXT NOT NULL,
            ProfileKey TEXT NOT NULL,
            ConnectionName TEXT NOT NULL,
            ProviderKind TEXT NOT NULL,
            ConnectionMode TEXT NOT NULL,
            ServerHost TEXT NULL,
            DatabaseName TEXT NULL,
            UserName TEXT NULL,
            IntegratedSecurity INTEGER NOT NULL DEFAULT 0,
            Encrypt INTEGER NOT NULL DEFAULT 1,
            TrustServerCertificate INTEGER NOT NULL DEFAULT 0,
            CommandTimeoutSec INTEGER NOT NULL DEFAULT 30,
            SecretRef TEXT NULL,
            IsActive INTEGER NOT NULL DEFAULT 0,
            Description TEXT NULL,
            ManagementMode TEXT NOT NULL DEFAULT 'UserManaged',
            CreatedUtc TEXT NOT NULL,
            UpdatedUtc TEXT NOT NULL,
            UNIQUE(EnvironmentName, ProfileKey, ConnectionName)
        );

        CREATE TABLE IF NOT EXISTS AppSecrets (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            SecretKey TEXT NOT NULL,
            CipherText TEXT NOT NULL,
            Description TEXT NULL,
            CreatedUtc TEXT NOT NULL,
            UpdatedUtc TEXT NOT NULL,
            UNIQUE(SecretKey)
        );

        CREATE TABLE IF NOT EXISTS Tenants (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            TenantKey TEXT NOT NULL,
            DisplayName TEXT NOT NULL,
            Description TEXT NULL,
            IsActive INTEGER NOT NULL DEFAULT 1,
            ManagementMode TEXT NOT NULL DEFAULT 'UserManaged',
            CreatedUtc TEXT NOT NULL,
            UpdatedUtc TEXT NOT NULL,
            UNIQUE(TenantKey)
        );

        CREATE TABLE IF NOT EXISTS TenantDomains (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            TenantId INTEGER NOT NULL,
            Domain TEXT NOT NULL,
            ConnectionName TEXT NOT NULL,
            SystemProfileKey TEXT NULL,
            IsDefault INTEGER NOT NULL DEFAULT 0,
            IsActive INTEGER NOT NULL DEFAULT 1,
            ManagementMode TEXT NOT NULL DEFAULT 'UserManaged',
            CreatedUtc TEXT NOT NULL,
            UpdatedUtc TEXT NOT NULL,
            FOREIGN KEY(TenantId) REFERENCES Tenants(Id),
            UNIQUE(TenantId, Domain)
        );

        CREATE TABLE IF NOT EXISTS PredictionProfiles (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Domain TEXT NOT NULL,
            ProfileKey TEXT NOT NULL,
            DisplayName TEXT NOT NULL,
            DomainPackKey TEXT NOT NULL,
            TargetMetricKey TEXT NOT NULL,
            CalendarProfileKey TEXT NOT NULL,
            Grain TEXT NOT NULL,
            Horizon INTEGER NOT NULL,
            HorizonUnit TEXT NOT NULL,
            ModelType TEXT NOT NULL,
            ConnectionName TEXT NULL,
            SourceMode TEXT NULL,
            TargetSeriesSource TEXT NULL,
            FeatureSourcesJson TEXT NULL,
            GroupByJson TEXT NULL,
            FiltersJson TEXT NULL,
            Notes TEXT NULL,
            IsActive INTEGER NOT NULL DEFAULT 1,
            CreatedUtc TEXT NOT NULL,
            UpdatedUtc TEXT NOT NULL,
            UNIQUE(Domain, ProfileKey)
        );
    ";

    await connection.ExecuteAsync(sql);
    await EnsureContextManagementSchemaAsync(connection);
    var createdUtc = DateTime.UtcNow.ToString("o");

    const string seedProfileSql = @"
        INSERT INTO SystemConfigProfiles
            (EnvironmentName, ProfileKey, DisplayName, Description, IsActive, IsReadOnly, CreatedUtc, UpdatedUtc)
        SELECT
            @EnvironmentName,
            @ProfileKey,
            @DisplayName,
            @Description,
            1,
            0,
            @CreatedUtc,
            @CreatedUtc
        WHERE NOT EXISTS (
            SELECT 1
            FROM SystemConfigProfiles
            WHERE EnvironmentName = @EnvironmentName
              AND ProfileKey = @ProfileKey
        );

        UPDATE SystemConfigProfiles
        SET IsActive = 1,
            UpdatedUtc = @CreatedUtc
        WHERE EnvironmentName = @EnvironmentName
          AND ProfileKey = @ProfileKey
          AND NOT EXISTS (
              SELECT 1
              FROM SystemConfigProfiles
              WHERE EnvironmentName = @EnvironmentName
                AND IsActive = 1
          );

        SELECT Id
        FROM SystemConfigProfiles
        WHERE EnvironmentName = @EnvironmentName
          AND ProfileKey = @ProfileKey;";

    var seededDefaultConnectionName = !string.IsNullOrWhiteSpace(configuration.GetConnectionString("ErpDb"))
        ? "ErpDb"
        : null;

    var profileId = await connection.ExecuteScalarAsync<int>(
        seedProfileSql,
        new
        {
            EnvironmentName = environmentName,
            ProfileKey = defaultSystemProfile,
            DisplayName = defaultSystemProfile,
            Description = $"Perfil operativo por defecto para {environmentName}.",
            CreatedUtc = createdUtc
        });

    var seedEntries = new (string Section, string Key, string? Value, string ValueType, string Description)[]
    {
        ("Paths", "Model", configuration["Paths:Model"], "string", "Ruta del modelo LLM mutable por perfil."),
        ("Docs", "RootPath", configuration["Docs:RootPath"] ?? configuration["Docs:WiRootPath"], "string", "Carpeta raiz de documentos PDF para indexacion."),
        ("Docs", "WiRootPath", configuration["Docs:WiRootPath"] ?? configuration["Docs:RootPath"], "string", "Compatibilidad temporal con el nombre legacy de la carpeta de WI."),
        ("Docs", "DefaultDomain", configuration["Docs:DefaultDomain"] ?? "work-instructions", "string", "Dominio por defecto usado por el pipeline de documentos."),
        ("Docs", "SchemaFile", configuration["Docs:SchemaFile"] ?? "work-instructions.json", "string", "Archivo JSON del schema de extraccion de documentos."),
        ("Docs", "TopKChunks", configuration["Docs:TopKChunks"] ?? configuration["Docs:TopKPages"] ?? "6", "int", "Cantidad de chunks a recuperar para respuestas de documentacion."),
        ("Docs", "TopKPages", configuration["Docs:TopKPages"] ?? configuration["Docs:TopKChunks"] ?? "6", "int", "Compatibilidad temporal con el nombre legacy de TopKChunks."),
        ("Docs", "MaxAnswerCitations", configuration["Docs:MaxAnswerCitations"], "int", "Maximo de citas devueltas por respuesta de documentacion.")
    };

    seedEntries =
    [
        .. seedEntries,
        ("Retrieval", "TopExamples", configuration["Settings:Retrieval:TopExamples"] ?? "10", "int", "Cantidad de training examples candidatos para retrieval."),
        ("Retrieval", "MinExampleScore", "2.5", "double", "Score mÃ­nimo para considerar un training example relevante."),
        ("Retrieval", "TopSchemaDocs", configuration["Settings:Retrieval:TopSchemaDocs"] ?? "6", "int", "Cantidad de schema docs relevantes a incluir."),
        ("Retrieval", "FallbackSchemaDocs", configuration["Settings:Retrieval:FallbackSchemaDocs"] ?? "15", "int", "Cantidad de schema docs de fallback cuando no hay match fuerte."),
        ("Retrieval", "Domain", configuration["Settings:Retrieval:Domain"] ?? "erp-kpi-pilot", "string", "Dominio operativo para retrieval y validaciÃ³n."),

        ("TenantDefaults", "TenantKey", "default", "string", "Tenant por defecto del runtime y onboarding inicial."),
        ("TenantDefaults", "ConnectionName", seededDefaultConnectionName, "string", "Nombre de conexiÃ³n por defecto para runtime y onboarding inicial."),
        ("Prompting", "MaxPromptChars", "9000", "int", "Presupuesto total del prompt SQL en caracteres."),
        ("Prompting", "MaxRulesChars", "1800", "int", "Presupuesto mÃ¡ximo para reglas de negocio en el prompt SQL."),
        ("Prompting", "MaxSemanticHintsChars", "1400", "int", "Presupuesto mÃ¡ximo para pistas semÃ¡nticas del dominio en el prompt SQL."),
        ("Prompting", "MaxSchemasChars", "1800", "int", "Presupuesto mÃ¡ximo para schema docs en el prompt SQL."),
        ("Prompting", "MaxExamplesChars", "4200", "int", "Presupuesto mÃ¡ximo para examples en el prompt SQL."),
        ("Prompting", "MaxRules", "6", "int", "Cantidad mÃ¡xima de business rules enviadas al prompt SQL."),
        ("Prompting", "MaxSemanticHints", "8", "int", "Cantidad mÃ¡xima de pistas semÃ¡nticas enviadas al prompt SQL."),
        ("Prompting", "MaxSchemas", "3", "int", "Cantidad mÃ¡xima de schema docs enviadas al prompt SQL."),
        ("Prompting", "MaxExamples", "2", "int", "Cantidad mÃ¡xima de training examples enviados al prompt SQL."),
        ("Prompting", "SystemPersona", "Eres un desarrollador experto en T-SQL para SQL Server.", "string", "Persona base del system prompt SQL."),
        ("Prompting", "TaskInstruction", "Tu tarea es generar SOLO codigo SQL valido para SQL Server.", "string", "InstrucciÃ³n principal del system prompt SQL."),
        ("Prompting", "ContextInstruction", "Debes basarte estrictamente en los objetos SQL permitidos, reglas, esquemas y ejemplos proporcionados.", "string", "InstrucciÃ³n de uso de contexto del system prompt SQL."),
        ("Prompting", "SqlSyntaxRules", "1. ESTA ESTRICTAMENTE PROHIBIDO USAR 'LIMIT'. Para limitar resultados en SQL Server, usa SIEMPRE 'SELECT TOP (N)'.\n2. Usa EXACTAMENTE los nombres de columnas que aparezcan en los esquemas recuperados y ejemplos validos.\n3. NUNCA compares un valor de texto contra una columna ID.\n4. Si necesitas cruzar objetos SQL permitidos, prefiere joins por IDs y OperationDate.\n5. Devuelve SOLO el SQL, sin comentarios y sin bloques markdown.", "string", "Bloque editable de reglas crÃ­ticas de sintaxis T-SQL."),
        ("Prompting", "TimeInterpretationRules", "- Hoy: CAST(OperationDate AS date) = CAST(GETDATE() AS date)\n- Ayer: CAST(OperationDate AS date) = DATEADD(DAY, -1, CAST(GETDATE() AS date))\n- Mes actual: YearMonth = CONVERT(char(7), GETDATE(), 120)\n- Semana actual: YearNumber = YEAR(GETDATE()) AND WeekOfYear = DATEPART(ISO_WEEK, GETDATE())\n- Cuando el usuario diga 'turno actual' o 'del turno', filtra explicitamente por un unico ShiftId calculado como el mas reciente del dia dentro de la vista consultada.", "string", "Bloque editable de interpretaciÃ³n temporal para el prompt SQL."),
        ("Prompting", "BusinessRulesHeader", "REGLAS DE NEGOCIO IMPORTANTES:", "string", "Encabezado para el bloque de business rules del prompt SQL."),
        ("Prompting", "SemanticHintsHeader", "PISTAS SEMANTICAS DEL DOMINIO:", "string", "Encabezado para el bloque de pistas semÃ¡nticas del prompt SQL."),
        ("Prompting", "AllowedObjectsHeader", "OBJETOS SQL PERMITIDOS:", "string", "Encabezado para el bloque de objetos permitidos del prompt SQL."),
        ("Prompting", "SchemasHeader", "ESQUEMAS RELEVANTES RECUPERADOS:", "string", "Encabezado para el bloque de schema docs del prompt SQL."),
        ("Prompting", "ExamplesHeader", "EJEMPLOS RELEVANTES:", "string", "Encabezado para el bloque de examples del prompt SQL."),
        ("Prompting", "QuestionHeader", "Pregunta actual:", "string", "Encabezado para la pregunta del usuario en el prompt SQL."),
        ("UiDefaults", "AdminDomain", configuration["Settings:Retrieval:Domain"] ?? "erp-kpi-pilot", "string", "Dominio por defecto para pantallas administrativas."),
        ("UiDefaults", "AdminTenant", "default", "string", "Tenant por defecto para pantallas administrativas.")
    ];

    const string seedEntrySql = @"
        INSERT INTO SystemConfigEntries
            (ProfileId, Section, [Key], Value, ValueType, IsSecret, SecretRef, IsEditableInUi, ValidationRule, Description, CreatedUtc, UpdatedUtc)
        SELECT
            @ProfileId,
            @Section,
            @Key,
            @Value,
            @ValueType,
            0,
            NULL,
            1,
            NULL,
            @Description,
            @CreatedUtc,
            @CreatedUtc
        WHERE NOT EXISTS (
            SELECT 1
            FROM SystemConfigEntries
            WHERE ProfileId = @ProfileId
              AND Section = @Section
              AND [Key] = @Key
        );";

    foreach (var entry in seedEntries.Where(x => !string.IsNullOrWhiteSpace(x.Value)))
    {
        await connection.ExecuteAsync(
            seedEntrySql,
            new
            {
                ProfileId = profileId,
                entry.Section,
                entry.Key,
                entry.Value,
                entry.ValueType,
                entry.Description,
                CreatedUtc = createdUtc
            });
    }

    const string seedPredictionProfileSql = @"
        INSERT INTO PredictionProfiles
            (Domain, ProfileKey, DisplayName, DomainPackKey, TargetMetricKey, CalendarProfileKey, Grain, Horizon, HorizonUnit, ModelType, ConnectionName, SourceMode, TargetSeriesSource, FeatureSourcesJson, GroupByJson, FiltersJson, Notes, IsActive, CreatedUtc, UpdatedUtc)
        SELECT
            @Domain,
            @ProfileKey,
            @DisplayName,
            @DomainPackKey,
            @TargetMetricKey,
            @CalendarProfileKey,
            @Grain,
            @Horizon,
            @HorizonUnit,
            @ModelType,
            @ConnectionName,
            @SourceMode,
            @TargetSeriesSource,
            @FeatureSourcesJson,
            @GroupByJson,
            @FiltersJson,
            @Notes,
            1,
            @CreatedUtc,
            @CreatedUtc
        WHERE NOT EXISTS (
            SELECT 1
            FROM PredictionProfiles
            WHERE Domain = @Domain
              AND ProfileKey = @ProfileKey
        );";

    await connection.ExecuteAsync(
        seedPredictionProfileSql,
        new
        {
            Domain = configuration["Settings:Retrieval:Domain"] ?? "erp-kpi-pilot",
            ProfileKey = "industrial-scrap-shift",
            DisplayName = "Industrial Scrap Shift Forecast",
            DomainPackKey = "industrial-kpi",
            TargetMetricKey = "scrap_qty",
            CalendarProfileKey = "shift-calendar",
            Grain = "shift",
            Horizon = 1,
            HorizonUnit = "shift",
            ModelType = "FastTree",
            ConnectionName = seededDefaultConnectionName,
            SourceMode = "KpiViews",
            TargetSeriesSource = "ml:active-profile",
            FeatureSourcesJson = "[\"produced_qty\",\"downtime_minutes\"]",
            GroupByJson = "[\"part\",\"shift\"]",
            FiltersJson = (string?)null,
            Notes = "Perfil semilla transicional para forecasting industrial por turno.",
            CreatedUtc = createdUtc
        });

    await connection.ExecuteAsync(
        seedPredictionProfileSql,
        new
        {
            Domain = "northwind-sales",
            ProfileKey = "northwind-sales-daily-units",
            DisplayName = "Northwind Sales Daily Units Forecast",
            DomainPackKey = "northwind-sales",
            TargetMetricKey = "units_sold",
            CalendarProfileKey = "standard-calendar",
            Grain = "day",
            Horizon = 7,
            HorizonUnit = "day",
            ModelType = "FastTree",
            ConnectionName = "NorthwindDb",
            SourceMode = "CustomSql",
            TargetSeriesSource = @"
SELECT
    CAST(od.ProductID AS nvarchar(50)) AS SeriesKey,
    CAST(o.OrderDate AS date) AS ObservedOn,
    1 AS BucketKey,
    'Daily' AS BucketLabel,
    CAST(0 AS bigint) AS BucketStartTick,
    CAST(863999999999 AS bigint) AS BucketEndTick,
    CAST(SUM(od.Quantity) AS float) AS TargetValue
FROM dbo.Orders o
JOIN dbo.OrderDetails od ON o.OrderID = od.OrderID
WHERE o.OrderDate IS NOT NULL
GROUP BY od.ProductID, CAST(o.OrderDate AS date)
ORDER BY od.ProductID, CAST(o.OrderDate AS date)",
            FeatureSourcesJson = "[\"net_sales\",\"order_count\"]",
            GroupByJson = "[\"product\"]",
            FiltersJson = (string?)null,
            Notes = "Perfil semilla para forecast de unidades vendidas por producto en Northwind.",
            CreatedUtc = createdUtc
        });

    const string seedTenantSql = @"
        INSERT INTO Tenants
            (TenantKey, DisplayName, Description, IsActive, ManagementMode, CreatedUtc, UpdatedUtc)
        SELECT
            @TenantKey,
            @DisplayName,
            @Description,
            1,
            @ManagementMode,
            @CreatedUtc,
            @CreatedUtc
        WHERE NOT EXISTS (
            SELECT 1
            FROM Tenants
            WHERE TenantKey = @TenantKey
        );

        SELECT Id
        FROM Tenants
        WHERE TenantKey = @TenantKey;";

    var defaultTenantId = await connection.ExecuteScalarAsync<int>(
        seedTenantSql,
        new
        {
            TenantKey = "default",
            DisplayName = "Default",
            Description = "Tenant por defecto para compatibilidad transicional.",
            ManagementMode = Tenant.SeedManagedMode,
            CreatedUtc = createdUtc
        });

    const string seedTenantDomainSql = @"
        INSERT INTO TenantDomains
            (TenantId, Domain, ConnectionName, SystemProfileKey, IsDefault, IsActive, ManagementMode, CreatedUtc, UpdatedUtc)
        SELECT
            @TenantId,
            @Domain,
            @ConnectionName,
            @SystemProfileKey,
            1,
            1,
            @ManagementMode,
            @CreatedUtc,
            @CreatedUtc
        WHERE NOT EXISTS (
            SELECT 1
            FROM TenantDomains
            WHERE TenantId = @TenantId
              AND Domain = @Domain
        );";

    if (!string.IsNullOrWhiteSpace(seededDefaultConnectionName))
    {
        await connection.ExecuteAsync(
            seedTenantDomainSql,
            new
            {
                TenantId = defaultTenantId,
                Domain = configuration["Settings:Retrieval:Domain"] ?? "erp-kpi-pilot",
                ConnectionName = seededDefaultConnectionName,
                SystemProfileKey = defaultSystemProfile,
                ManagementMode = Tenant.SeedManagedMode,
                CreatedUtc = createdUtc
            });
    }
    Console.WriteLine($"[DB Setup] System config listo en: {sqlitePath}");
}

// =========================================================
// PREPARACION DEL ENTORNO DE RUNTIME (SQLite)
// =========================================================
static async Task EnsureRuntimeDatabaseSetupAsync(IServiceProvider services)
{
    var options = services.GetRequiredService<RuntimeDbOptions>();

    using var connection = new SqliteConnection($"Data Source={options.DbPath};");
    await connection.OpenAsync();

    const string sql = @"
        CREATE TABLE IF NOT EXISTS QuestionJobs (
            JobId TEXT PRIMARY KEY,
            UserId TEXT NOT NULL,
            Role TEXT NOT NULL,
            TenantKey TEXT NOT NULL DEFAULT 'default',
            Domain TEXT NULL,
            ConnectionName TEXT NOT NULL DEFAULT '',
            Question TEXT NOT NULL,
            Status TEXT NOT NULL,
            Mode TEXT NOT NULL DEFAULT 'Data',
            SqlText TEXT NULL,
            ErrorText TEXT NULL,
            ResultJson TEXT NULL,
            Attempt INTEGER NOT NULL DEFAULT 0,
            TrainingExampleSaved INTEGER NOT NULL DEFAULT 0,
            VerificationStatus TEXT NOT NULL DEFAULT 'Pending',
            UserFeedback TEXT NULL,
            FeedbackUtc TEXT NULL,
            FeedbackComment TEXT NULL,
            CreatedUtc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
            UpdatedUtc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
        );

        CREATE INDEX IF NOT EXISTS IX_QuestionJobs_CreatedUtc
            ON QuestionJobs(CreatedUtc DESC);

        CREATE INDEX IF NOT EXISTS IX_QuestionJobs_UserQuestionContextStatus
            ON QuestionJobs(UserId, Question, TenantKey, Domain, ConnectionName, Status);

        CREATE TABLE IF NOT EXISTS LlmRuntimeProfile (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            IsActive INTEGER NOT NULL DEFAULT 0,
            Name TEXT NOT NULL,
            GpuLayerCount INTEGER NULL,
            ContextSize INTEGER NULL,
            Threads INTEGER NULL,
            BatchThreads INTEGER NULL,
            BatchSize INTEGER NULL,
            UBatchSize INTEGER NULL,
            FlashAttention INTEGER NULL,
            UseMemorymap INTEGER NOT NULL DEFAULT 1,
            NoKqvOffload INTEGER NOT NULL DEFAULT 0,
            OpOffload INTEGER NULL,
            CreatedUtc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
            UpdatedUtc TEXT NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS IX_LlmRuntimeProfile_SingleActive
            ON LlmRuntimeProfile(IsActive)
            WHERE IsActive = 1;
    ";

    await connection.ExecuteAsync(sql);
    await EnsureLlmRuntimeProfileSchemaAsync(connection);

    const string seedProfileSql = @"
        INSERT INTO LlmRuntimeProfile
            (IsActive, Name, GpuLayerCount, ContextSize, Threads, BatchThreads, BatchSize, UBatchSize, FlashAttention, UseMemorymap, NoKqvOffload, OpOffload, CreatedUtc)
        SELECT
            1,
            'Fallback-Workstation',
            15,
            2048,
            NULL,
            NULL,
            128,
            64,
            NULL,
            1,
            0,
            NULL,
            @CreatedUtc
        WHERE NOT EXISTS (
            SELECT 1
            FROM LlmRuntimeProfile
        );";

    await connection.ExecuteAsync(
        seedProfileSql,
        new
        {
            CreatedUtc = DateTime.UtcNow.ToString("o")
        });
}

static async Task EnsureLlmRuntimeProfileSchemaAsync(SqliteConnection connection)
{
    var columns = (await connection.QueryAsync<string>(
        "SELECT name FROM pragma_table_info('LlmRuntimeProfile');"))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    if (columns.Count == 0)
        return;

    var alterStatements = new List<string>();

    if (!columns.Contains("IsActive"))
        alterStatements.Add("ALTER TABLE LlmRuntimeProfile ADD COLUMN IsActive INTEGER NOT NULL DEFAULT 0;");
    if (!columns.Contains("Name"))
        alterStatements.Add("ALTER TABLE LlmRuntimeProfile ADD COLUMN Name TEXT NOT NULL DEFAULT 'Default';");
    if (!columns.Contains("GpuLayerCount"))
        alterStatements.Add("ALTER TABLE LlmRuntimeProfile ADD COLUMN GpuLayerCount INTEGER NULL;");
    if (!columns.Contains("ContextSize"))
        alterStatements.Add("ALTER TABLE LlmRuntimeProfile ADD COLUMN ContextSize INTEGER NULL;");
    if (!columns.Contains("Threads"))
        alterStatements.Add("ALTER TABLE LlmRuntimeProfile ADD COLUMN Threads INTEGER NULL;");
    if (!columns.Contains("BatchThreads"))
        alterStatements.Add("ALTER TABLE LlmRuntimeProfile ADD COLUMN BatchThreads INTEGER NULL;");
    if (!columns.Contains("BatchSize"))
        alterStatements.Add("ALTER TABLE LlmRuntimeProfile ADD COLUMN BatchSize INTEGER NULL;");
    if (!columns.Contains("UBatchSize"))
        alterStatements.Add("ALTER TABLE LlmRuntimeProfile ADD COLUMN UBatchSize INTEGER NULL;");
    if (!columns.Contains("FlashAttention"))
        alterStatements.Add("ALTER TABLE LlmRuntimeProfile ADD COLUMN FlashAttention INTEGER NULL;");
    if (!columns.Contains("UseMemorymap"))
        alterStatements.Add("ALTER TABLE LlmRuntimeProfile ADD COLUMN UseMemorymap INTEGER NOT NULL DEFAULT 1;");
    if (!columns.Contains("NoKqvOffload"))
        alterStatements.Add("ALTER TABLE LlmRuntimeProfile ADD COLUMN NoKqvOffload INTEGER NOT NULL DEFAULT 0;");
    if (!columns.Contains("OpOffload"))
        alterStatements.Add("ALTER TABLE LlmRuntimeProfile ADD COLUMN OpOffload INTEGER NULL;");
    if (!columns.Contains("CreatedUtc"))
        alterStatements.Add("ALTER TABLE LlmRuntimeProfile ADD COLUMN CreatedUtc TEXT NULL;");
    if (!columns.Contains("UpdatedUtc"))
        alterStatements.Add("ALTER TABLE LlmRuntimeProfile ADD COLUMN UpdatedUtc TEXT NULL;");

    foreach (var statement in alterStatements)
    {
        await connection.ExecuteAsync(statement);
    }

    await connection.ExecuteAsync(@"
        UPDATE LlmRuntimeProfile
        SET CreatedUtc = COALESCE(CreatedUtc, CURRENT_TIMESTAMP)
        WHERE CreatedUtc IS NULL;

        CREATE UNIQUE INDEX IF NOT EXISTS IX_LlmRuntimeProfile_SingleActive
            ON LlmRuntimeProfile(IsActive)
            WHERE IsActive = 1;");
}

static async Task EnsureMemoryFeatureDatabaseSetupAsync(string sqlitePath)
{
    using var connection = new SqliteConnection($"Data Source={sqlitePath};");
    await connection.OpenAsync();

    const string sql = @"
        CREATE TABLE IF NOT EXISTS BusinessRules (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Domain TEXT NOT NULL,
            RuleKey TEXT NOT NULL,
            RuleText TEXT NOT NULL,
            Priority INTEGER NOT NULL DEFAULT 100,
            IsActive INTEGER NOT NULL DEFAULT 1,
            CreatedUtc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
            UpdatedUtc TEXT NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS UX_BusinessRules_Domain_RuleKey
            ON BusinessRules(Domain, RuleKey);

        CREATE INDEX IF NOT EXISTS IX_BusinessRules_Domain_IsActive_Priority
            ON BusinessRules(Domain, IsActive, Priority, Id);

        CREATE TABLE IF NOT EXISTS AllowedObjects (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Domain TEXT NOT NULL,
            SchemaName TEXT NOT NULL,
            ObjectName TEXT NOT NULL,
            ObjectType TEXT NOT NULL DEFAULT '',
            IsActive INTEGER NOT NULL DEFAULT 1,
            Notes TEXT NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS UX_AllowedObjects_Domain_Schema_Object
            ON AllowedObjects(Domain, SchemaName, ObjectName);

        CREATE INDEX IF NOT EXISTS IX_AllowedObjects_Domain_IsActive
            ON AllowedObjects(Domain, IsActive, SchemaName, ObjectName);

        CREATE TABLE IF NOT EXISTS SchemaDocs (
            SchemaName TEXT NOT NULL,
            TableName TEXT NOT NULL,
            DocText TEXT NOT NULL,
            JsonDefinition TEXT NOT NULL,
            PRIMARY KEY (SchemaName, TableName)
        );

        CREATE TABLE IF NOT EXISTS TrainingExamples (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Question TEXT NOT NULL,
            Sql TEXT NOT NULL,
            TenantKey TEXT NOT NULL DEFAULT '',
            Domain TEXT NOT NULL DEFAULT '',
            ConnectionName TEXT NOT NULL DEFAULT '',
            IntentName TEXT NULL,
            IsVerified INTEGER NOT NULL DEFAULT 0,
            Priority INTEGER NOT NULL DEFAULT 0,
            CreatedUtc DATETIME NOT NULL,
            LastUsedUtc DATETIME NOT NULL,
            UseCount INTEGER NOT NULL DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS ReviewQueue (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Question TEXT NOT NULL,
            GeneratedSql TEXT NOT NULL,
            ErrorMessage TEXT NULL,
            Status TEXT NOT NULL,
            Reason TEXT NOT NULL,
            CreatedUtc DATETIME NOT NULL
        );

        CREATE TABLE IF NOT EXISTS SemanticHints (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Domain TEXT NOT NULL,
            HintKey TEXT NOT NULL,
            HintType TEXT NOT NULL,
            DisplayName TEXT NULL,
            ObjectName TEXT NULL,
            ColumnName TEXT NULL,
            HintText TEXT NOT NULL,
            Priority INTEGER NOT NULL DEFAULT 100,
            IsActive INTEGER NOT NULL DEFAULT 1,
            CreatedUtc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
            UpdatedUtc TEXT NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS IX_SemanticHints_Domain_HintKey
            ON SemanticHints(Domain, HintKey);

        CREATE INDEX IF NOT EXISTS IX_SemanticHints_Domain_IsActive_Priority
            ON SemanticHints(Domain, IsActive, Priority, Id);

        CREATE TABLE IF NOT EXISTS QueryPatterns (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Domain TEXT NOT NULL,
            PatternKey TEXT NOT NULL,
            IntentName TEXT NOT NULL,
            Description TEXT NULL,
            SqlTemplate TEXT NOT NULL,
            DefaultTopN INTEGER NULL,
            MetricKey TEXT NULL,
            DimensionKey TEXT NULL,
            DefaultTimeScopeKey TEXT NULL,
            Priority INTEGER NOT NULL DEFAULT 100,
            IsActive INTEGER NOT NULL DEFAULT 1,
            CreatedUtc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
            UpdatedUtc TEXT NULL
        );

        CREATE INDEX IF NOT EXISTS IX_QueryPatterns_Domain_IsActive_Priority
            ON QueryPatterns(Domain, IsActive, Priority, Id);

        CREATE TABLE IF NOT EXISTS QueryPatternTerms (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            PatternId INTEGER NOT NULL,
            Term TEXT NOT NULL,
            TermGroup TEXT NOT NULL,
            MatchMode TEXT NOT NULL DEFAULT 'contains',
            IsRequired INTEGER NOT NULL DEFAULT 1,
            IsActive INTEGER NOT NULL DEFAULT 1,
            CreatedUtc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
            FOREIGN KEY (PatternId) REFERENCES QueryPatterns(Id)
        );

        CREATE INDEX IF NOT EXISTS IX_QueryPatternTerms_PatternId_IsActive
            ON QueryPatternTerms(PatternId, IsActive, IsRequired, Id);

        CREATE TABLE IF NOT EXISTS DocDocuments (
            DocId TEXT PRIMARY KEY,
            Domain TEXT NOT NULL,
            FileName TEXT NOT NULL,
            UpdatedUtc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
        );

        CREATE INDEX IF NOT EXISTS IX_DocDocuments_Domain_UpdatedUtc
            ON DocDocuments(Domain, UpdatedUtc DESC);

        CREATE TABLE IF NOT EXISTS DocChunks (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            DocId TEXT NOT NULL,
            PageNumber INTEGER NOT NULL,
            Text TEXT NOT NULL,
            FOREIGN KEY (DocId) REFERENCES DocDocuments(DocId)
        );

        CREATE INDEX IF NOT EXISTS IX_DocChunks_DocId_PageNumber
            ON DocChunks(DocId, PageNumber);
    ";

    await connection.ExecuteAsync(sql);
    await EnsureDocumentSchemaAsync(connection);
    await EnsureTrainingExamplesSchemaAsync(connection);
}

static async Task EnsureDocumentSchemaAsync(SqliteConnection connection)
{
    var docDocumentColumns = (await connection.QueryAsync<string>(
        "SELECT name FROM pragma_table_info('DocDocuments');"))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    if (docDocumentColumns.Count > 0)
    {
        if (!docDocumentColumns.Contains("FilePath"))
            await connection.ExecuteAsync("ALTER TABLE DocDocuments ADD COLUMN FilePath TEXT NULL;");
        if (!docDocumentColumns.Contains("Sha256"))
            await connection.ExecuteAsync("ALTER TABLE DocDocuments ADD COLUMN Sha256 TEXT NULL;");
        if (!docDocumentColumns.Contains("PageCount"))
            await connection.ExecuteAsync("ALTER TABLE DocDocuments ADD COLUMN PageCount INTEGER NOT NULL DEFAULT 0;");
        if (!docDocumentColumns.Contains("DocumentType"))
            await connection.ExecuteAsync("ALTER TABLE DocDocuments ADD COLUMN DocumentType TEXT NULL;");
        if (!docDocumentColumns.Contains("Title"))
            await connection.ExecuteAsync("ALTER TABLE DocDocuments ADD COLUMN Title TEXT NULL;");
    }

    var docChunkColumns = (await connection.QueryAsync<string>(
        "SELECT name FROM pragma_table_info('DocChunks');"))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    if (docChunkColumns.Count > 0)
    {
        if (!docChunkColumns.Contains("ChunkKey"))
            await connection.ExecuteAsync("ALTER TABLE DocChunks ADD COLUMN ChunkKey TEXT NULL;");
        if (!docChunkColumns.Contains("ChunkOrder"))
            await connection.ExecuteAsync("ALTER TABLE DocChunks ADD COLUMN ChunkOrder INTEGER NOT NULL DEFAULT 1;");
        if (!docChunkColumns.Contains("ChunkTitle"))
            await connection.ExecuteAsync("ALTER TABLE DocChunks ADD COLUMN ChunkTitle TEXT NULL;");
        if (!docChunkColumns.Contains("SectionName"))
            await connection.ExecuteAsync("ALTER TABLE DocChunks ADD COLUMN SectionName TEXT NULL;");
        if (!docChunkColumns.Contains("PartNumbers"))
            await connection.ExecuteAsync("ALTER TABLE DocChunks ADD COLUMN PartNumbers TEXT NULL;");
        if (!docChunkColumns.Contains("NormalizedTokens"))
            await connection.ExecuteAsync("ALTER TABLE DocChunks ADD COLUMN NormalizedTokens TEXT NULL;");
        if (!docChunkColumns.Contains("TokenCount"))
            await connection.ExecuteAsync("ALTER TABLE DocChunks ADD COLUMN TokenCount INTEGER NOT NULL DEFAULT 0;");
        if (!docChunkColumns.Contains("IsCoverPage"))
            await connection.ExecuteAsync("ALTER TABLE DocChunks ADD COLUMN IsCoverPage INTEGER NOT NULL DEFAULT 0;");
        if (!docChunkColumns.Contains("UpdatedUtc"))
            await connection.ExecuteAsync("ALTER TABLE DocChunks ADD COLUMN UpdatedUtc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP;");
    }

    await connection.ExecuteAsync("CREATE UNIQUE INDEX IF NOT EXISTS IX_DocChunks_ChunkKey ON DocChunks(ChunkKey);");
    await connection.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_DocChunks_DocId_PageNumber_Order ON DocChunks(DocId, PageNumber, ChunkOrder);");
    await connection.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_DocDocuments_Domain_UpdatedUtc ON DocDocuments(Domain, UpdatedUtc DESC);");
}

static async Task EnsureTrainingExamplesSchemaAsync(SqliteConnection connection)
{
    var columns = (await connection.QueryAsync<string>(
        "SELECT name FROM pragma_table_info('TrainingExamples');"))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    if (columns.Count == 0)
    {
        const string createTableSql = @"
            CREATE TABLE IF NOT EXISTS TrainingExamples (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Question TEXT NOT NULL,
                Sql TEXT NOT NULL,
                TenantKey TEXT NOT NULL DEFAULT '',
                Domain TEXT NOT NULL DEFAULT '',
                ConnectionName TEXT NOT NULL DEFAULT '',
                IntentName TEXT NULL,
                IsVerified INTEGER NOT NULL DEFAULT 0,
                Priority INTEGER NOT NULL DEFAULT 0,
                CreatedUtc DATETIME NOT NULL,
                LastUsedUtc DATETIME NOT NULL,
                UseCount INTEGER NOT NULL DEFAULT 0
            );";

        await connection.ExecuteAsync(createTableSql);
    }
    else
    {
        var needsMigration =
            !columns.Contains("TenantKey") ||
            !columns.Contains("ConnectionName") ||
            await HasLegacyTrainingExamplesUniqueConstraintAsync(connection);

        if (needsMigration)
        {
            var tenantKeySelect = columns.Contains("TenantKey") ? "COALESCE(TenantKey, '')" : "''";
            var domainSelect = columns.Contains("Domain") ? "COALESCE(Domain, '')" : "''";
            var connectionNameSelect = columns.Contains("ConnectionName") ? "COALESCE(ConnectionName, '')" : "''";
            var intentNameSelect = columns.Contains("IntentName") ? "IntentName" : "NULL";
            var isVerifiedSelect = columns.Contains("IsVerified") ? "COALESCE(IsVerified, 0)" : "0";
            var prioritySelect = columns.Contains("Priority") ? "COALESCE(Priority, 0)" : "0";
            var createdUtcSelect = columns.Contains("CreatedUtc") ? "COALESCE(CreatedUtc, CURRENT_TIMESTAMP)" : "CURRENT_TIMESTAMP";
            var lastUsedUtcSelect = columns.Contains("LastUsedUtc") ? "COALESCE(LastUsedUtc, CURRENT_TIMESTAMP)" : "CURRENT_TIMESTAMP";
            var useCountSelect = columns.Contains("UseCount") ? "COALESCE(UseCount, 0)" : "0";

            var migrationSql = $@"
                BEGIN IMMEDIATE TRANSACTION;

                DROP TABLE IF EXISTS TrainingExamples_new;

                CREATE TABLE TrainingExamples_new (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Question TEXT NOT NULL,
                    Sql TEXT NOT NULL,
                    TenantKey TEXT NOT NULL DEFAULT '',
                    Domain TEXT NOT NULL DEFAULT '',
                    ConnectionName TEXT NOT NULL DEFAULT '',
                    IntentName TEXT NULL,
                    IsVerified INTEGER NOT NULL DEFAULT 0,
                    Priority INTEGER NOT NULL DEFAULT 0,
                    CreatedUtc DATETIME NOT NULL,
                    LastUsedUtc DATETIME NOT NULL,
                    UseCount INTEGER NOT NULL DEFAULT 0
                );

                INSERT INTO TrainingExamples_new
                    (Id, Question, Sql, TenantKey, Domain, ConnectionName, IntentName, IsVerified, Priority, CreatedUtc, LastUsedUtc, UseCount)
                SELECT
                    Id,
                    Question,
                    Sql,
                    {tenantKeySelect},
                    {domainSelect},
                    {connectionNameSelect},
                    {intentNameSelect},
                    {isVerifiedSelect},
                    {prioritySelect},
                    {createdUtcSelect},
                    {lastUsedUtcSelect},
                    {useCountSelect}
                FROM TrainingExamples;

                DROP TABLE IF EXISTS TrainingExamples;
                ALTER TABLE TrainingExamples_new RENAME TO TrainingExamples;

                COMMIT;";

            await connection.ExecuteAsync(migrationSql);
        }
    }

    const string indexSql = @"
        CREATE UNIQUE INDEX IF NOT EXISTS IX_TrainingExamples_ContextQuestion
            ON TrainingExamples(Question, TenantKey, Domain, ConnectionName);

        CREATE INDEX IF NOT EXISTS IX_TrainingExamples_ContextIntentVerified
            ON TrainingExamples(TenantKey, Domain, ConnectionName, IntentName, IsVerified, Priority DESC);";

    await connection.ExecuteAsync(indexSql);
}

static async Task<bool> HasLegacyTrainingExamplesUniqueConstraintAsync(SqliteConnection connection)
{
    const string tableSql = @"
        SELECT sql
        FROM sqlite_master
        WHERE type = 'table'
          AND name = 'TrainingExamples';";

    var createSql = await connection.ExecuteScalarAsync<string?>(tableSql);

    if (!string.IsNullOrWhiteSpace(createSql) &&
        createSql.Contains("Question TEXT NOT NULL UNIQUE", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    const string indexSql = @"
        SELECT name
        FROM pragma_index_list('TrainingExamples')
        WHERE [unique] = 1;";

    var uniqueIndexes = (await connection.QueryAsync<string>(indexSql)).ToList();

    return uniqueIndexes.Any(name =>
        string.Equals(name, "IX_TrainingExamples_Question", StringComparison.OrdinalIgnoreCase));
}

static async Task EnsureSeededConnectionProfilesAndContextMappingsAsync(
    string sqlitePath,
    IConfiguration configuration,
    string environmentName,
    string defaultSystemProfile)
{
    using var connection = new SqliteConnection($"Data Source={sqlitePath};");
    await connection.OpenAsync();

    var now = DateTime.UtcNow.ToString("o");
    var pruneToSeeded = configuration.GetValue("PilotContexts:PruneToSeeded", true);
    var configuredSeeds =
        configuration.GetSection("PilotContexts:OverrideMappings").Get<List<PilotContextSeed>>() ??
        configuration.GetSection("PilotContexts:Mappings").Get<List<PilotContextSeed>>() ??
        [];
    if (configuredSeeds.Count == 0)
    {
        configuredSeeds =
        [
            new PilotContextSeed("default", "Default", "Workspace principal del piloto ERP.", "erp-kpi-pilot", "ErpDb", true),
            new PilotContextSeed("northwind-demo", "NorthWind Demo", "Workspace demo Northwind para validación local.", "northwind-sales", "NorthwindDb", true)
        ];
    }
    configuredSeeds = configuredSeeds
        .Where(seed => !string.IsNullOrWhiteSpace(seed.TenantKey)
            && !string.IsNullOrWhiteSpace(seed.Domain)
            && !string.IsNullOrWhiteSpace(seed.ConnectionName))
        .GroupBy(seed => $"{seed.TenantKey.Trim().ToLowerInvariant()}|{seed.Domain.Trim().ToLowerInvariant()}")
        .Select(group => group.First() with
        {
            TenantKey = group.First().TenantKey.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(group.First().DisplayName) ? group.First().TenantKey.Trim() : group.First().DisplayName.Trim(),
            Description = string.IsNullOrWhiteSpace(group.First().Description) ? null : group.First().Description.Trim(),
            Domain = group.First().Domain.Trim(),
            ConnectionName = group.First().ConnectionName.Trim()
        })
        .ToList();

    Console.WriteLine(
        $"[PilotContexts] PruneToSeeded={pruneToSeeded} | Seeds={string.Join(", ", configuredSeeds.Select(x => $"{x.TenantKey}/{x.Domain}/{x.ConnectionName}"))}");

    async Task UpsertConnectionProfileFromConfigAsync(string connectionName, string? description = null)
    {
        var connectionString = configuration.GetConnectionString(connectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var builder = new SqlConnectionStringBuilder(connectionString);
        var encryptOption = builder.Encrypt.ToString();
        var encryptFlag = !string.Equals(encryptOption, "Optional", StringComparison.OrdinalIgnoreCase);

        const string upsertProfileSql = @"
            INSERT INTO ConnectionProfiles
                (EnvironmentName, ProfileKey, ConnectionName, ProviderKind, ConnectionMode, ServerHost, DatabaseName, UserName,
                 IntegratedSecurity, Encrypt, TrustServerCertificate, CommandTimeoutSec, SecretRef, IsActive, Description, ManagementMode, CreatedUtc, UpdatedUtc)
            VALUES
                (@EnvironmentName, @ProfileKey, @ConnectionName, @ProviderKind, @ConnectionMode, @ServerHost, @DatabaseName, @UserName,
                 @IntegratedSecurity, @Encrypt, @TrustServerCertificate, @CommandTimeoutSec, @SecretRef, @IsActive, @Description, @ManagementMode, @CreatedUtc, @UpdatedUtc)
            ON CONFLICT(EnvironmentName, ProfileKey, ConnectionName)
            DO UPDATE SET
                ProviderKind = excluded.ProviderKind,
                ConnectionMode = excluded.ConnectionMode,
                ServerHost = excluded.ServerHost,
                DatabaseName = excluded.DatabaseName,
                UserName = excluded.UserName,
                IntegratedSecurity = excluded.IntegratedSecurity,
                Encrypt = excluded.Encrypt,
                TrustServerCertificate = excluded.TrustServerCertificate,
                CommandTimeoutSec = excluded.CommandTimeoutSec,
                SecretRef = excluded.SecretRef,
                IsActive = excluded.IsActive,
                Description = excluded.Description,
                ManagementMode = excluded.ManagementMode,
                UpdatedUtc = excluded.UpdatedUtc;";

        await connection.ExecuteAsync(
            upsertProfileSql,
            new
            {
                EnvironmentName = environmentName,
                ProfileKey = defaultSystemProfile,
                ConnectionName = connectionName,
                ProviderKind = "SqlServer",
                ConnectionMode = "FullStringRef",
                ServerHost = builder.DataSource,
                DatabaseName = builder.InitialCatalog,
                UserName = string.IsNullOrWhiteSpace(builder.UserID) ? null : builder.UserID,
                IntegratedSecurity = builder.IntegratedSecurity,
                Encrypt = encryptFlag,
                TrustServerCertificate = builder.TrustServerCertificate,
                CommandTimeoutSec = builder.ConnectTimeout <= 0 ? 30 : builder.ConnectTimeout,
                SecretRef = $"config:ConnectionStrings:{connectionName}",
                IsActive = true,
                Description = description ?? $"{connectionName} configurada desde ConnectionStrings.",
                ManagementMode = Tenant.SeedManagedMode,
                CreatedUtc = now,
                UpdatedUtc = now
            });
    }

    async Task<long?> UpsertTenantAsync(PilotContextSeed seed)
    {
        var existingTenant = await connection.QueryFirstOrDefaultAsync<Tenant>(
            """
            SELECT *
            FROM Tenants
            WHERE TenantKey = @tenantKey
            LIMIT 1;
            """,
            new { tenantKey = seed.TenantKey });

        const string upsertTenantSql = @"
            INSERT INTO Tenants
                (TenantKey, DisplayName, Description, IsActive, ManagementMode, CreatedUtc, UpdatedUtc)
            VALUES
                (@TenantKey, @DisplayName, @Description, 1, @ManagementMode, @CreatedUtc, @UpdatedUtc)
            ON CONFLICT(TenantKey)
            DO UPDATE SET
                DisplayName = excluded.DisplayName,
                Description = excluded.Description,
                IsActive = 1,
                ManagementMode = excluded.ManagementMode,
                UpdatedUtc = excluded.UpdatedUtc;

            SELECT Id
            FROM Tenants
            WHERE TenantKey = @TenantKey
            LIMIT 1;";

        return await connection.ExecuteScalarAsync<long?>(
            upsertTenantSql,
            new
            {
                TenantKey = seed.TenantKey,
                DisplayName = seed.DisplayName,
                Description = seed.Description,
                ManagementMode = Tenant.SeedManagedMode,
                CreatedUtc = existingTenant?.CreatedUtc ?? now,
                UpdatedUtc = now
            });
    }

    async Task RemapTenantDomainAsync(long tenantId, PilotContextSeed seed)
    {
        var existing = await connection.QueryFirstOrDefaultAsync<TenantDomain>(
            """
            SELECT *
            FROM TenantDomains
            WHERE TenantId = @tenantId
              AND Domain = @domain
            LIMIT 1;
            """,
            new { tenantId, domain = seed.Domain });

        const string upsertTenantDomainSql = @"
            UPDATE TenantDomains
            SET IsDefault = 0,
                UpdatedUtc = @UpdatedUtc
            WHERE TenantId = @TenantId
              AND Domain <> @Domain
              AND @IsDefault = 1;

            INSERT INTO TenantDomains
                (TenantId, Domain, ConnectionName, SystemProfileKey, IsDefault, IsActive, ManagementMode, CreatedUtc, UpdatedUtc)
            VALUES
                (@TenantId, @Domain, @ConnectionName, @SystemProfileKey, @IsDefault, @IsActive, @ManagementMode, @CreatedUtc, @UpdatedUtc)
            ON CONFLICT(TenantId, Domain)
            DO UPDATE SET
                ConnectionName = excluded.ConnectionName,
                SystemProfileKey = excluded.SystemProfileKey,
                IsDefault = excluded.IsDefault,
                IsActive = excluded.IsActive,
                ManagementMode = excluded.ManagementMode,
                UpdatedUtc = excluded.UpdatedUtc;";

        await connection.ExecuteAsync(
            upsertTenantDomainSql,
            new
            {
                TenantId = tenantId,
                Domain = seed.Domain,
                ConnectionName = seed.ConnectionName,
                SystemProfileKey = defaultSystemProfile,
                IsDefault = seed.IsDefault,
                IsActive = true,
                ManagementMode = Tenant.SeedManagedMode,
                CreatedUtc = existing?.CreatedUtc ?? now,
                UpdatedUtc = now
            });
    }

    var activeConnectionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var activeTenantKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var activeMappingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var seed in configuredSeeds)
    {
        var connectionString = configuration.GetConnectionString(seed.ConnectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.WriteLine($"[PilotContexts] Omitiendo seed '{seed.TenantKey}/{seed.Domain}' porque no existe ConnectionStrings:{seed.ConnectionName}.");
            continue;
        }

        await UpsertConnectionProfileFromConfigAsync(seed.ConnectionName, seed.Description);
        var tenantId = await UpsertTenantAsync(seed);
        if (tenantId is null)
            continue;

        await RemapTenantDomainAsync(tenantId.Value, seed);

        activeConnectionNames.Add(seed.ConnectionName);
        activeTenantKeys.Add(seed.TenantKey);
        activeMappingKeys.Add($"{seed.TenantKey}|{seed.Domain}");
    }

    if (!pruneToSeeded)
    {
        Console.WriteLine("[PilotContexts] Prune desactivado; se conservaran contextos locales heredados.");
        return;
    }

    var runtimeMappings = await connection.QueryAsync<(long Id, string TenantKey, string Domain, string ManagementMode)>(
        """
        SELECT td.Id, t.TenantKey, td.Domain, td.ManagementMode
        FROM TenantDomains td
        INNER JOIN Tenants t ON t.Id = td.TenantId;
        """);

    foreach (var mapping in runtimeMappings)
    {
        if (!string.Equals(mapping.ManagementMode, Tenant.SeedManagedMode, StringComparison.OrdinalIgnoreCase))
            continue;

        var key = $"{mapping.TenantKey}|{mapping.Domain}";
        if (activeMappingKeys.Contains(key))
            continue;

        await connection.ExecuteAsync(
            """
            UPDATE TenantDomains
            SET IsActive = 0,
                IsDefault = 0,
                UpdatedUtc = @UpdatedUtc
            WHERE Id = @Id;
            """,
            new { Id = mapping.Id, UpdatedUtc = now });
    }

    var tenantRows = await connection.QueryAsync<(long Id, string TenantKey, string ManagementMode)>(
        """
        SELECT Id, TenantKey, ManagementMode
        FROM Tenants;
        """);

    foreach (var tenant in tenantRows)
    {
        if (!string.Equals(tenant.ManagementMode, Tenant.SeedManagedMode, StringComparison.OrdinalIgnoreCase))
            continue;

        var keepActive = activeTenantKeys.Contains(tenant.TenantKey);
        await connection.ExecuteAsync(
            """
            UPDATE Tenants
            SET IsActive = @IsActive,
                UpdatedUtc = @UpdatedUtc
            WHERE Id = @Id;
            """,
            new { Id = tenant.Id, IsActive = keepActive, UpdatedUtc = now });
    }

    var profiles = await connection.QueryAsync<(long Id, string ConnectionName, string ManagementMode)>(
        """
        SELECT Id, ConnectionName, ManagementMode
        FROM ConnectionProfiles
        WHERE EnvironmentName = @EnvironmentName
          AND ProfileKey = @ProfileKey;
        """,
        new { EnvironmentName = environmentName, ProfileKey = defaultSystemProfile });

    foreach (var profile in profiles)
    {
        if (!string.Equals(profile.ManagementMode, Tenant.SeedManagedMode, StringComparison.OrdinalIgnoreCase))
            continue;

        var keepActive = activeConnectionNames.Contains(profile.ConnectionName);
        await connection.ExecuteAsync(
            """
            UPDATE ConnectionProfiles
            SET IsActive = @IsActive,
                UpdatedUtc = @UpdatedUtc
            WHERE Id = @Id;
            """,
            new { Id = profile.Id, IsActive = keepActive, UpdatedUtc = now });
    }

    var dormantUserManagedContexts = await connection.QueryAsync<(long TenantId, string TenantKey, long MappingId, string Domain, string ConnectionName, bool IsDefault)>(
        """
        SELECT t.Id AS TenantId,
               t.TenantKey,
               td.Id AS MappingId,
               td.Domain,
               td.ConnectionName,
               td.IsDefault
        FROM Tenants t
        INNER JOIN TenantDomains td ON td.TenantId = t.Id
        INNER JOIN ConnectionProfiles cp ON cp.ConnectionName = td.ConnectionName
        WHERE t.ManagementMode = 'UserManaged'
          AND td.ManagementMode = 'UserManaged'
          AND cp.EnvironmentName = @EnvironmentName
          AND cp.ProfileKey = @ProfileKey
          AND cp.IsActive = 1
          AND (t.IsActive = 0 OR td.IsActive = 0);
        """,
        new { EnvironmentName = environmentName, ProfileKey = defaultSystemProfile });

    foreach (var dormant in dormantUserManagedContexts)
    {
        await connection.ExecuteAsync(
            """
            UPDATE Tenants
            SET IsActive = 1,
                UpdatedUtc = @UpdatedUtc
            WHERE Id = @TenantId;

            UPDATE TenantDomains
            SET IsActive = 1,
                UpdatedUtc = @UpdatedUtc
            WHERE Id = @MappingId;
            """,
            new
            {
                dormant.TenantId,
                dormant.MappingId,
                UpdatedUtc = now
            });

        Console.WriteLine($"[PilotContexts] Reactivado contexto local user-managed '{dormant.TenantKey}/{dormant.Domain}/{dormant.ConnectionName}'.");
    }

    Console.WriteLine(
        $"[PilotContexts] Activos en esta maquina => Workspaces: {string.Join(", ", activeTenantKeys.OrderBy(x => x))} | Contextos: {string.Join(", ", activeMappingKeys.OrderBy(x => x))}");
}

static async Task EnsureQueryPatternTimeScopeSeedsAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();

    var systemConfigProvider = scope.ServiceProvider.GetRequiredService<ISystemConfigProvider>();
    var queryPatternStore = scope.ServiceProvider.GetRequiredService<IQueryPatternStore>();
    var queryPatternTermStore = scope.ServiceProvider.GetRequiredService<IQueryPatternTermStore>();

    var domain = await systemConfigProvider.GetValueAsync("Retrieval", "Domain", ct: CancellationToken.None);
    if (string.IsNullOrWhiteSpace(domain))
        domain = "erp-kpi-pilot";

    if (string.IsNullOrWhiteSpace(domain))
        return;

    var patterns = await queryPatternStore.GetAllAsync(domain.Trim(), CancellationToken.None);
    if (patterns.Count == 0)
        return;

    var timeScopeTerms = new (string Group, string Term, string MatchMode)[]
    {
        ("time_scope_current_shift", "turno actual", "contains"),
        ("time_scope_current_shift", "del turno", "contains"),
        ("time_scope_current_shift", "turno en curso", "contains"),
        ("time_scope_today", "hoy", "contains"),
        ("time_scope_today", "dia de hoy", "contains"),
        ("time_scope_today", "today", "contains"),
        ("time_scope_yesterday", "ayer", "contains"),
        ("time_scope_yesterday", "yesterday", "contains"),
        ("time_scope_current_week", "esta semana", "contains"),
        ("time_scope_current_week", "semana actual", "contains"),
        ("time_scope_current_week", "de la semana", "contains"),
        ("time_scope_current_week", "current week", "contains"),
        ("time_scope_current_month", "este mes", "contains"),
        ("time_scope_current_month", "mes actual", "contains"),
        ("time_scope_current_month", "del mes", "contains"),
        ("time_scope_current_month", "current month", "contains")
    };

    foreach (var pattern in patterns.Where(x => x.Id > 0))
    {
        foreach (var seed in timeScopeTerms)
        {
            await queryPatternTermStore.UpsertAsync(
                new QueryPatternTerm
                {
                    PatternId = pattern.Id,
                    Term = seed.Term,
                    TermGroup = seed.Group,
                    MatchMode = seed.MatchMode,
                    IsRequired = false,
                    IsActive = true
                },
                CancellationToken.None);
        }
    }
}

static async Task EnsureCorePilotQueryPatternSeedsAsync(IServiceProvider services, IConfiguration configuration)
{
    using var scope = services.CreateScope();

    var queryPatternStore = scope.ServiceProvider.GetRequiredService<IQueryPatternStore>();
    var queryPatternTermStore = scope.ServiceProvider.GetRequiredService<IQueryPatternTermStore>();

    var configuredSeeds =
        configuration.GetSection("PilotContexts:OverrideMappings").Get<List<PilotContextSeed>>() ??
        configuration.GetSection("PilotContexts:Mappings").Get<List<PilotContextSeed>>() ??
        [];

    if (configuredSeeds.Count == 0)
    {
        configuredSeeds =
        [
            new PilotContextSeed("default", "Default", "Workspace principal del piloto ERP.", "erp-kpi-pilot", "ErpDb", true),
            new PilotContextSeed("northwind-demo", "NorthWind Demo", "Workspace demo Northwind para validación local.", "northwind-sales", "NorthwindDb", true)
        ];
    }

    var erpDomains = configuredSeeds
        .Where(x => !string.IsNullOrWhiteSpace(x.Domain) &&
                    x.Domain.Contains("erp", StringComparison.OrdinalIgnoreCase))
        .Select(x => x.Domain.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (erpDomains.Count == 0)
        return;

    foreach (var domain in erpDomains)
    {
        var topScrapByPressId = await queryPatternStore.UpsertAsync(
            new QueryPattern
            {
                Domain = domain,
                PatternKey = "top_scrap_by_press",
                IntentName = "top_scrap_by_press",
                Description = "Top N de prensas con mayor scrap para preguntas demo del piloto ERP.",
                SqlTemplate = "{SqlBuilderFallback}",
                DefaultTopN = 5,
                MetricKey = "scrapqty",
                DimensionKey = "press",
                DefaultTimeScopeKey = "current_shift",
                Priority = 25,
                IsActive = true
            },
            CancellationToken.None);

        var topScrapByPartNumberId = await queryPatternStore.UpsertAsync(
            new QueryPattern
            {
                Domain = domain,
                PatternKey = "top_scrap_by_partnumber",
                IntentName = "top_scrap_by_partnumber",
                Description = "Top N de números de parte con mayor scrap para preguntas demo del piloto ERP.",
                SqlTemplate = "{SqlBuilderFallback}",
                DefaultTopN = 5,
                MetricKey = "scrapqty",
                DimensionKey = "partnumber",
                DefaultTimeScopeKey = "today",
                Priority = 20,
                IsActive = true
            },
            CancellationToken.None);

        var topScrapByPressTerms = new (string Term, string Group, bool Required)[]
        {
            ("scrap", "metric_scrap", true),
            ("prensa", "dimension_press", true),
            ("prensas", "dimension_press", true),
            ("press", "dimension_press", true),
            ("mas", "ranking_top", false),
            ("con mas", "ranking_top", false),
            ("top", "ranking_top", false)
        };

        foreach (var term in topScrapByPressTerms)
        {
            await queryPatternTermStore.UpsertAsync(
                new QueryPatternTerm
                {
                    PatternId = topScrapByPressId,
                    Term = term.Term,
                    TermGroup = term.Group,
                    MatchMode = "contains",
                    IsRequired = term.Required,
                    IsActive = true
                },
                CancellationToken.None);
        }

        var topScrapByPartTerms = new (string Term, string Group, bool Required)[]
        {
            ("scrap", "metric_scrap", true),
            ("numero de parte", "dimension_partnumber", true),
            ("numeros de parte", "dimension_partnumber", true),
            ("número de parte", "dimension_partnumber", true),
            ("números de parte", "dimension_partnumber", true),
            ("part number", "dimension_partnumber", true),
            ("part numbers", "dimension_partnumber", true),
            ("mas", "ranking_top", false),
            ("con mas", "ranking_top", false),
            ("top", "ranking_top", false)
        };

        foreach (var term in topScrapByPartTerms)
        {
            await queryPatternTermStore.UpsertAsync(
                new QueryPatternTerm
                {
                    PatternId = topScrapByPartNumberId,
                    Term = term.Term,
                    TermGroup = term.Group,
                    MatchMode = "contains",
                    IsRequired = term.Required,
                    IsActive = true
                },
                CancellationToken.None);
        }
    }
}

static async Task EnsureCorePilotSemanticHintSeedsAsync(IServiceProvider services, IConfiguration configuration, string sqlitePath)
{
    using var scope = services.CreateScope();

    var semanticHintStore = scope.ServiceProvider.GetRequiredService<ISemanticHintStore>();

    var configuredSeeds =
        configuration.GetSection("PilotContexts:OverrideMappings").Get<List<PilotContextSeed>>() ??
        configuration.GetSection("PilotContexts:Mappings").Get<List<PilotContextSeed>>() ??
        [];

    if (configuredSeeds.Count == 0)
    {
        configuredSeeds =
        [
            new PilotContextSeed("default", "Default", "Workspace principal del piloto ERP.", "erp-kpi-pilot", "ErpDb", true),
            new PilotContextSeed("northwind-demo", "NorthWind Demo", "Workspace demo Northwind para validaciÃ³n local.", "northwind-sales", "NorthwindDb", true)
        ];
    }

    var erpDomains = configuredSeeds
        .Where(x => !string.IsNullOrWhiteSpace(x.Domain) &&
                    x.Domain.Contains("erp", StringComparison.OrdinalIgnoreCase))
        .Select(x => x.Domain.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (erpDomains.Count == 0)
        return;

    foreach (var domain in erpDomains)
    {
        var seeds = new[]
        {
            new SemanticHint
            {
                Domain = domain,
                HintKey = "scrap_view_metric_scrapqty",
                HintType = "metric",
                DisplayName = "ScrapQty",
                ObjectName = "dbo.vw_KpiScrap_v1",
                ColumnName = "ScrapQty",
                HintText = "Para preguntas de scrap en dbo.vw_KpiScrap_v1 usa la columna ScrapQty como mÃ©trica principal. No uses Qty ni ScrapQuantity.",
                Priority = 5,
                IsActive = true
            },
            new SemanticHint
            {
                Domain = domain,
                HintKey = "scrap_view_dimension_partnumber",
                HintType = "dimension",
                DisplayName = "PartNumber",
                ObjectName = "dbo.vw_KpiScrap_v1",
                ColumnName = "PartNumber",
                HintText = "Para preguntas de scrap por nÃºmero de parte en dbo.vw_KpiScrap_v1 agrupa por PartNumber.",
                Priority = 6,
                IsActive = true
            },
            new SemanticHint
            {
                Domain = domain,
                HintKey = "scrap_view_time_operationdate",
                HintType = "time",
                DisplayName = "OperationDate",
                ObjectName = "dbo.vw_KpiScrap_v1",
                ColumnName = "OperationDate",
                HintText = "La fecha operativa de dbo.vw_KpiScrap_v1 es OperationDate. Para 'hoy' usa CAST(OperationDate AS date) = CAST(GETDATE() AS date).",
                Priority = 7,
                IsActive = true
            },
            new SemanticHint
            {
                Domain = domain,
                HintKey = "scrap_view_shift_shiftid",
                HintType = "time",
                DisplayName = "ShiftId",
                ObjectName = "dbo.vw_KpiScrap_v1",
                ColumnName = "ShiftId",
                HintText = "Cuando el usuario pregunte por 'turno actual' o 'del turno' en dbo.vw_KpiScrap_v1, filtra por el ShiftId mÃ¡s reciente del dÃ­a.",
                Priority = 8,
                IsActive = true
            }
        };

        foreach (var seed in seeds)
        {
            await semanticHintStore.UpsertAsync(sqlitePath, seed, CancellationToken.None);
        }
    }
}

static async Task ReportPilotContextMemoryHealthAsync(string sqlitePath, IConfiguration configuration)
{
    using var connection = new SqliteConnection($"Data Source={sqlitePath};");
    await connection.OpenAsync();

    var configuredSeeds =
        configuration.GetSection("PilotContexts:OverrideMappings").Get<List<PilotContextSeed>>() ??
        configuration.GetSection("PilotContexts:Mappings").Get<List<PilotContextSeed>>() ??
        [];

    if (configuredSeeds.Count == 0)
    {
        configuredSeeds =
        [
            new PilotContextSeed("default", "Default", "Workspace principal del piloto ERP.", "erp-kpi-pilot", "ErpDb", true),
            new PilotContextSeed("northwind-demo", "NorthWind Demo", "Workspace demo Northwind para validación local.", "northwind-sales", "NorthwindDb", true)
        ];
    }

    var domains = configuredSeeds
        .Where(x => !string.IsNullOrWhiteSpace(x.Domain))
        .Select(x => x.Domain.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (domains.Count == 0)
        return;

    async Task<bool> TableHasColumnAsync(string tableName, string columnName)
    {
        var sql = $"SELECT name FROM pragma_table_info('{tableName}') WHERE name = @ColumnName LIMIT 1;";
        var found = await connection.ExecuteScalarAsync<string?>(sql, new { ColumnName = columnName });
        return !string.IsNullOrWhiteSpace(found);
    }

    async Task<long?> CountByDomainAsync(string tableName, string domain, bool requireActive = false)
    {
        if (!await TableHasColumnAsync(tableName, "Domain"))
            return null;

        var hasIsActive = requireActive && await TableHasColumnAsync(tableName, "IsActive");
        var sql = hasIsActive
            ? $"SELECT COUNT(1) FROM {tableName} WHERE Domain = @Domain AND IsActive = 1;"
            : $"SELECT COUNT(1) FROM {tableName} WHERE Domain = @Domain;";

        return await connection.ExecuteScalarAsync<long>(sql, new { Domain = domain });
    }

    foreach (var domain in domains)
    {
        var allowedObjectsCount = await CountByDomainAsync("AllowedObjects", domain, requireActive: true);
        var schemaDocsCount = await CountByDomainAsync("SchemaDocs", domain);
        var semanticHintsCount = await CountByDomainAsync("SemanticHints", domain, requireActive: true);
        var businessRulesCount = await CountByDomainAsync("BusinessRules", domain, requireActive: true);
        var queryPatternsCount = await CountByDomainAsync("QueryPatterns", domain, requireActive: true);
        var trainingExamplesCount = await CountByDomainAsync("TrainingExamples", domain);

        Console.WriteLine(
            $"[PilotMemory] {domain} => Allowed={FormatHealthCount(allowedObjectsCount)}, SchemaDocs={FormatHealthCount(schemaDocsCount)}, Hints={FormatHealthCount(semanticHintsCount)}, Rules={FormatHealthCount(businessRulesCount)}, Patterns={FormatHealthCount(queryPatternsCount)}, Examples={FormatHealthCount(trainingExamplesCount)}");

        if (allowedObjectsCount is null)
        {
            Console.WriteLine(
                $"[PilotMemory][WARN] El dominio '{domain}' usa una tabla legacy sin columna Domain en AllowedObjects. No se pudo validar salud completa de memoria para este ambiente.");
            continue;
        }

        if (allowedObjectsCount == 0)
        {
            Console.WriteLine(
                $"[PilotMemory][WARN] El dominio '{domain}' está activo pero no tiene AllowedObjects. El carril SQL fallará hasta re-sembrar onboarding o restaurar la memoria local de esta máquina.");
        }

        if (allowedObjectsCount > 0 && (schemaDocsCount ?? 0) == 0 && (semanticHintsCount ?? 0) == 0)
        {
            Console.WriteLine(
                $"[PilotMemory][WARN] El dominio '{domain}' tiene AllowedObjects pero no tiene SchemaDocs ni SemanticHints. SQL puede funcionar, pero con peor calidad hasta inicializar el dominio.");
        }
    }
}

static string FormatHealthCount(long? value) => value?.ToString() ?? "legacy-schema";





