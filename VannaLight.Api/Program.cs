using Dapper;
using LLama.Native;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using VannaLight.Api.Hubs;
using VannaLight.Api.Services;
using VannaLight.Api.Services.Predictions;
using VannaLight.Core.Abstractions;
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

var modelRelOrAbs = builder.Configuration["Paths:Model"] ?? @"C:\Modelos\qwen2.5-coder-7b-instruct-q4_k_m.gguf";
var modelPath = Path.IsPathRooted(modelRelOrAbs)
    ? modelRelOrAbs
    : Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, modelRelOrAbs));

Directory.CreateDirectory(Path.GetDirectoryName(sqlitePath)!);
Directory.CreateDirectory(Path.GetDirectoryName(runtimePath)!);

// ---------------------------------------------------------
// 2. CONFIGURACION DE ARRANQUE
// ---------------------------------------------------------
var environmentName = builder.Configuration["SystemStartup:EnvironmentName"] ?? builder.Environment.EnvironmentName;
var defaultSystemProfile = builder.Configuration["SystemStartup:DefaultSystemProfile"] ?? "default";

// ---------------------------------------------------------
// 3. REGISTRO DE CONFIGURACIONES BASE
// ---------------------------------------------------------
var operationalConn = builder.Configuration.GetConnectionString("OperationalDb");

if (string.IsNullOrWhiteSpace(operationalConn))
    throw new InvalidOperationException("Falta configurar ConnectionStrings:OperationalDb.");

var sqliteOptions = new SqliteOptions(sqlitePath);
var runtimeOptions = new RuntimeDbOptions(runtimePath);
var operationalOptions = new OperationalDbOptions(operationalConn);

// Registro directo de options concretas
builder.Services.AddSingleton(sqliteOptions);
builder.Services.AddSingleton(runtimeOptions);
builder.Services.AddSingleton(operationalOptions);

// Compatibilidad para servicios que usen IOptions<T>
builder.Services.AddSingleton<IOptions<SqliteOptions>>(Options.Create(sqliteOptions));
builder.Services.AddSingleton<IOptions<OperationalDbOptions>>(Options.Create(operationalOptions));
builder.Services.AddSingleton<IOptions<RuntimeDbOptions>>(Options.Create(runtimeOptions));

// AppSettings actual: se mantiene por compatibilidad hasta mover LlmClient
var settings = AppSettingsFactory.Create(RuntimeProfile.ALTO, modelPath);
builder.Services.AddSingleton(settings);

// ---------------------------------------------------------
// 4. NUEVA CAPA DE CONFIGURACION OPERATIVA
// ---------------------------------------------------------
builder.Services.AddSingleton<ISystemConfigStore>(_ => new SqliteSystemConfigStore(sqlitePath));
builder.Services.AddSingleton<IConnectionProfileStore>(_ => new SqliteConnectionProfileStore(sqlitePath));
builder.Services.AddSingleton<ISystemConfigProvider, SystemConfigProvider>();
builder.Services.AddSingleton<ISecretResolver, CompositeSecretResolver>();
builder.Services.AddSingleton<IOperationalConnectionResolver, OperationalConnectionResolver>();

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
builder.Services.AddSingleton<IAllowedObjectStore, SqliteAllowedObjectStore>();
builder.Services.AddSingleton<ISqlCacheService, SqlCacheService>();

// Ingesta de esquema
builder.Services.AddSingleton<ISchemaIngestor, SqlServerSchemaIngestor>();

// Pattern-first
builder.Services.AddSingleton<IPatternMatcherService, PatternMatcherService>();
builder.Services.AddSingleton<ITemplateSqlBuilder, TemplateSqlBuilder>();

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
await EnsureSystemConfigDatabaseSetupAsync(sqlitePath);
await EnsureRuntimeDatabaseSetupAsync(app.Services);

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
static async Task EnsureSystemConfigDatabaseSetupAsync(string sqlitePath)
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
    ";

    await connection.ExecuteAsync(sql);
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