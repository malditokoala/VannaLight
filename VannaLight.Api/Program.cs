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
// 2. REGISTRO DE CONFIGURACIONES
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

var settings = AppSettingsFactory.Create(RuntimeProfile.ALTO, modelPath);
builder.Services.AddSingleton(settings);

// ---------------------------------------------------------
// 3. IA Y CONFIGURACION DE INFERENCIA
// ---------------------------------------------------------
builder.Services.AddSingleton<ILlmRuntimeProfileProvider, SqliteLlmProfileProvider>();
builder.Services.AddSingleton<ILlmProfileStore, SqliteLlmProfileStore>();
builder.Services.AddSingleton<ILlmClient, LlmClient>();

// ---------------------------------------------------------
// 4. SERVICIOS BASE E INFRAESTRUCTURA
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
// 5. DOCS (RAG) Y MACHINE LEARNING
// Se mantienen registrados, aunque no son la prioridad actual
// ---------------------------------------------------------
builder.Services.AddSingleton<WiDocIngestor>();
builder.Services.AddSingleton<IDocsIntentRouter, DocsIntentRouterLlm>();
builder.Services.AddSingleton<IDocsAnswerService, DocsAnswerService>();

builder.Services.AddSingleton<IPredictionIntentRouter, PredictionIntentRouterLlm>();
builder.Services.AddSingleton<IForecastingService, ForecastingService>();
builder.Services.AddSingleton<IPredictionAnswerService, PredictionAnswerService>();

// ---------------------------------------------------------
// 6. WORKER DE INFERENCIA
// ---------------------------------------------------------
builder.Services.AddSingleton<IAskRequestQueue, AskRequestQueue>();
builder.Services.AddHostedService<InferenceWorker>();

var app = builder.Build();

// ---------------------------------------------------------
// 7. INICIALIZACION DE BASES DE DATOS LOCALES
// ---------------------------------------------------------
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
// PREPARACION DEL ENTORNO DE RUNTIME (SQLite)
// =========================================================
static async Task EnsureRuntimeDatabaseSetupAsync(IServiceProvider services)
{
    var options = services.GetRequiredService<RuntimeDbOptions>();

    using var connection = new SqliteConnection($"Data Source={options.DbPath};");
    await connection.OpenAsync();

    const string sql = @"
        -- =====================================================
        -- QuestionJobs: estado e historial de trabajos
        -- =====================================================
        CREATE TABLE IF NOT EXISTS QuestionJobs (
            JobId TEXT PRIMARY KEY,
            UserId TEXT NOT NULL,
            Role TEXT NOT NULL,
            Question TEXT NOT NULL,
            Status TEXT NOT NULL,
            Mode TEXT NOT NULL DEFAULT 'Data',
            CreatedUtc TEXT NOT NULL,
            UpdatedUtc TEXT NOT NULL,
            SqlText TEXT,
            ErrorText TEXT,
            ResultJson TEXT,
            Attempt INTEGER NOT NULL DEFAULT 0,
            TrainingExampleSaved INTEGER NOT NULL DEFAULT 0,
            VerificationStatus TEXT DEFAULT 'Pending',
            FeedbackComment TEXT
        );

        CREATE INDEX IF NOT EXISTS IX_QuestionJobs_User_Created
            ON QuestionJobs(UserId, CreatedUtc DESC);

        -- =====================================================
        -- ReviewQueue: cola de revision humana
        -- =====================================================
        CREATE TABLE IF NOT EXISTS ReviewQueue (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Question TEXT NOT NULL,
            GeneratedSql TEXT NOT NULL,
            ErrorMessage TEXT,
            Status TEXT NOT NULL,
            Reason TEXT NOT NULL,
            CreatedUtc DATETIME NOT NULL
        );

        CREATE INDEX IF NOT EXISTS IX_ReviewQueue_Status_CreatedUtc
            ON ReviewQueue(Status, CreatedUtc ASC);

        -- =====================================================
        -- LlmRuntimeProfile: perfiles de inferencia
        -- =====================================================
        CREATE TABLE IF NOT EXISTS LlmRuntimeProfile (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            IsActive INTEGER NOT NULL DEFAULT 0,
            ContextSize INTEGER,
            GpuLayerCount INTEGER,
            Threads INTEGER,
            BatchThreads INTEGER,
            BatchSize INTEGER,
            UBatchSize INTEGER,
            FlashAttention INTEGER DEFAULT 0,
            UseMemorymap INTEGER DEFAULT 1,
            NoKqvOffload INTEGER DEFAULT 0,
            OpOffload INTEGER DEFAULT 1,
            UpdatedUtc TEXT NOT NULL
        );

        -- =====================================================
        -- QueryAudit: observabilidad y trazabilidad
        -- =====================================================
        CREATE TABLE IF NOT EXISTS QueryAudit (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            JobId TEXT NULL,
            Question TEXT NOT NULL,
            RouteUsed TEXT NULL,
            GeneratedSql TEXT NULL,
            ExecutionTimeMs INTEGER NULL,
            RowsReturned INTEGER NULL,
            Status TEXT NOT NULL,
            ErrorMessage TEXT NULL,
            UserFeedback TEXT NULL,
            CreatedAt TEXT NOT NULL
        );

        -- =====================================================
        -- Presets iniciales de perfiles de inferencia
        -- =====================================================
        INSERT INTO LlmRuntimeProfile
            (Name, IsActive, GpuLayerCount, ContextSize, BatchSize, UBatchSize, UpdatedUtc)
        SELECT 'Home-RTX4060', 0, 35, 4096, 256, 128, DATETIME('now')
        WHERE NOT EXISTS (
            SELECT 1 FROM LlmRuntimeProfile WHERE Name = 'Home-RTX4060'
        );

        INSERT INTO LlmRuntimeProfile
            (Name, IsActive, GpuLayerCount, ContextSize, BatchSize, UBatchSize, UpdatedUtc)
        SELECT 'Work-QuadroT2000', 1, 15, 2048, 128, 64, DATETIME('now')
        WHERE NOT EXISTS (
            SELECT 1 FROM LlmRuntimeProfile WHERE Name = 'Work-QuadroT2000'
        );
    ";

    await connection.ExecuteAsync(sql);
    Console.WriteLine($"[DB Setup] Entorno de Runtime listo en: {options.DbPath}");
}