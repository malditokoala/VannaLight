using Dapper;
using LLama.Native;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using VannaLight.Api.Hubs;
using VannaLight.Api.Services;
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

// Carga de secretos en desarrollo
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

// ---------------------------------------------------------
// 1. GESTION DE RUTAS Y DIRECTORIOS (ContentRoot)
// ---------------------------------------------------------
var sqliteRel = builder.Configuration["Paths:Sqlite"] ?? "Data/vanna_memory.db";
var sqlitePath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, sqliteRel));

var runtimeRel = builder.Configuration["Paths:RuntimeDb"] ?? "Data/vanna_runtime.db";
var runtimePath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, runtimeRel));
var dataProtectionKeysPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "Data", "dpkeys"));

var modelRelOrAbs = builder.Configuration["Paths:Model"] ?? @"C:\Modelos\qwen2.5-coder-7b-instruct-q4_k_m.gguf";
var modelPath = Path.IsPathRooted(modelRelOrAbs)
    ? modelRelOrAbs
    : Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, modelRelOrAbs));

Directory.CreateDirectory(Path.GetDirectoryName(sqlitePath)!);
Directory.CreateDirectory(Path.GetDirectoryName(runtimePath)!);
Directory.CreateDirectory(dataProtectionKeysPath);

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

// Registro directo de options concretas
builder.Services.AddSingleton(sqliteOptions);
builder.Services.AddSingleton(runtimeOptions);
builder.Services.AddSingleton<OperationalDbOptions>(sp =>
{
    var connectionString = sp.GetRequiredService<IOperationalConnectionResolver>()
        .ResolveOperationalConnectionStringAsync()
        .GetAwaiter()
        .GetResult();

    return new OperationalDbOptions(connectionString);
});

// Compatibilidad para servicios que usen IOptions<T>
builder.Services.AddSingleton<IOptions<SqliteOptions>>(Options.Create(sqliteOptions));
builder.Services.AddSingleton<IOptions<OperationalDbOptions>>(sp =>
    Options.Create(sp.GetRequiredService<OperationalDbOptions>()));
builder.Services.AddSingleton<IOptions<RuntimeDbOptions>>(Options.Create(runtimeOptions));

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
builder.Services.AddSingleton<WiDocIngestor>();
builder.Services.AddSingleton<IDocsIntentRouter, DocsIntentRouterLlm>();
builder.Services.AddSingleton<IDocsAnswerService, DocsAnswerService>();

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
await EnsureRuntimeDatabaseSetupAsync(app.Services);
await EnsureQueryPatternTimeScopeSeedsAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.UseAuthorization();

app.MapControllers();
app.MapHub<AssistantHub>("/hub/assistant");

app.Run();

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
            CreatedUtc TEXT NOT NULL,
            UpdatedUtc TEXT NOT NULL,
            FOREIGN KEY(TenantId) REFERENCES Tenants(Id),
            UNIQUE(TenantId, Domain)
        );
    ";

    await connection.ExecuteAsync(sql);
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
        ("Docs", "WiRootPath", configuration["Docs:WiRootPath"], "string", "Carpeta de documentos WI para reindexación."),
        ("Docs", "TopKPages", configuration["Docs:TopKPages"], "int", "Cantidad de páginas a recuperar para respuestas de documentación."),
        ("Docs", "MaxAnswerCitations", configuration["Docs:MaxAnswerCitations"], "int", "Máximo de citas devueltas por respuesta de documentación.")
    };

    seedEntries =
    [
        .. seedEntries,
        ("Retrieval", "TopExamples", configuration["Settings:Retrieval:TopExamples"] ?? "10", "int", "Cantidad de training examples candidatos para retrieval."),
        ("Retrieval", "MinExampleScore", "2.5", "double", "Score mínimo para considerar un training example relevante."),
        ("Retrieval", "TopSchemaDocs", configuration["Settings:Retrieval:TopSchemaDocs"] ?? "6", "int", "Cantidad de schema docs relevantes a incluir."),
        ("Retrieval", "FallbackSchemaDocs", configuration["Settings:Retrieval:FallbackSchemaDocs"] ?? "15", "int", "Cantidad de schema docs de fallback cuando no hay match fuerte."),
        ("Retrieval", "Domain", configuration["Settings:Retrieval:Domain"] ?? "erp-kpi-pilot", "string", "Dominio operativo para retrieval y validación."),
        ("TenantDefaults", "TenantKey", "default", "string", "Tenant por defecto del runtime y onboarding inicial."),
        ("TenantDefaults", "ConnectionName", "OperationalDb", "string", "Nombre de conexión por defecto para runtime y onboarding inicial."),
        ("Prompting", "MaxPromptChars", "9000", "int", "Presupuesto total del prompt SQL en caracteres."),
        ("Prompting", "MaxRulesChars", "1800", "int", "Presupuesto máximo para reglas de negocio en el prompt SQL."),
        ("Prompting", "MaxSemanticHintsChars", "1400", "int", "Presupuesto máximo para pistas semánticas del dominio en el prompt SQL."),
        ("Prompting", "MaxSchemasChars", "1800", "int", "Presupuesto máximo para schema docs en el prompt SQL."),
        ("Prompting", "MaxExamplesChars", "4200", "int", "Presupuesto máximo para examples en el prompt SQL."),
        ("Prompting", "MaxRules", "6", "int", "Cantidad máxima de business rules enviadas al prompt SQL."),
        ("Prompting", "MaxSemanticHints", "8", "int", "Cantidad máxima de pistas semánticas enviadas al prompt SQL."),
        ("Prompting", "MaxSchemas", "3", "int", "Cantidad máxima de schema docs enviadas al prompt SQL."),
        ("Prompting", "MaxExamples", "2", "int", "Cantidad máxima de training examples enviados al prompt SQL."),
        ("Prompting", "SystemPersona", "Eres un desarrollador experto en T-SQL para SQL Server.", "string", "Persona base del system prompt SQL."),
        ("Prompting", "TaskInstruction", "Tu tarea es generar SOLO codigo SQL valido para SQL Server.", "string", "Instrucción principal del system prompt SQL."),
        ("Prompting", "ContextInstruction", "Debes basarte estrictamente en los objetos SQL permitidos, reglas, esquemas y ejemplos proporcionados.", "string", "Instrucción de uso de contexto del system prompt SQL."),
        ("Prompting", "SqlSyntaxRules", "1. ESTA ESTRICTAMENTE PROHIBIDO USAR 'LIMIT'. Para limitar resultados en SQL Server, usa SIEMPRE 'SELECT TOP (N)'.\n2. Usa EXACTAMENTE los nombres de columnas que aparezcan en los esquemas recuperados y ejemplos validos.\n3. NUNCA compares un valor de texto contra una columna ID.\n4. Si necesitas cruzar objetos SQL permitidos, prefiere joins por IDs y OperationDate.\n5. Devuelve SOLO el SQL, sin comentarios y sin bloques markdown.", "string", "Bloque editable de reglas críticas de sintaxis T-SQL."),
        ("Prompting", "TimeInterpretationRules", "- Hoy: CAST(OperationDate AS date) = CAST(GETDATE() AS date)\n- Ayer: CAST(OperationDate AS date) = DATEADD(DAY, -1, CAST(GETDATE() AS date))\n- Mes actual: YearMonth = CONVERT(char(7), GETDATE(), 120)\n- Semana actual: YearNumber = YEAR(GETDATE()) AND WeekOfYear = DATEPART(ISO_WEEK, GETDATE())\n- Cuando el usuario diga 'turno actual' o 'del turno', filtra explicitamente por un unico ShiftId calculado como el mas reciente del dia dentro de la vista consultada.", "string", "Bloque editable de interpretación temporal para el prompt SQL."),
        ("Prompting", "BusinessRulesHeader", "REGLAS DE NEGOCIO IMPORTANTES:", "string", "Encabezado para el bloque de business rules del prompt SQL."),
        ("Prompting", "SemanticHintsHeader", "PISTAS SEMANTICAS DEL DOMINIO:", "string", "Encabezado para el bloque de pistas sem�nticas del prompt SQL."),
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

    const string seedTenantSql = @"
        INSERT INTO Tenants
            (TenantKey, DisplayName, Description, IsActive, CreatedUtc, UpdatedUtc)
        SELECT
            @TenantKey,
            @DisplayName,
            @Description,
            1,
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
            CreatedUtc = createdUtc
        });

    const string seedTenantDomainSql = @"
        INSERT INTO TenantDomains
            (TenantId, Domain, ConnectionName, SystemProfileKey, IsDefault, IsActive, CreatedUtc, UpdatedUtc)
        SELECT
            @TenantId,
            @Domain,
            @ConnectionName,
            @SystemProfileKey,
            1,
            1,
            @CreatedUtc,
            @CreatedUtc
        WHERE NOT EXISTS (
            SELECT 1
            FROM TenantDomains
            WHERE TenantId = @TenantId
              AND Domain = @Domain
        );";

    await connection.ExecuteAsync(
        seedTenantDomainSql,
        new
        {
            TenantId = defaultTenantId,
            Domain = configuration["Settings:Retrieval:Domain"] ?? "erp-kpi-pilot",
            ConnectionName = "OperationalDb",
            SystemProfileKey = defaultSystemProfile,
            CreatedUtc = createdUtc
        });
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
        -- tu SQL actual de runtime aquí...
    ";

    await connection.ExecuteAsync(sql);
}

static async Task EnsureQueryPatternTimeScopeSeedsAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();

    var systemConfigProvider = scope.ServiceProvider.GetRequiredService<ISystemConfigProvider>();
    var queryPatternStore = scope.ServiceProvider.GetRequiredService<IQueryPatternStore>();
    var queryPatternTermStore = scope.ServiceProvider.GetRequiredService<IQueryPatternTermStore>();

    var domain = await systemConfigProvider.GetValueAsync("Retrieval", "Domain", CancellationToken.None);
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


