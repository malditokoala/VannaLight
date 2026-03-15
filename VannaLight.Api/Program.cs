using Dapper;
using LLama.Native;
using Microsoft.Data.Sqlite;
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

// =========================================================
// CONFIGURACIÓN NATIVA DE LLamaSharp
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
// 1. GESTIÓN DE RUTAS Y DIRECTORIOS (ContentRoot)
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
var operationalConn = builder.Configuration.GetConnectionString("OperationalDb")
    ?? throw new InvalidOperationException("Falta ConnectionStrings:OperationalDb.");

builder.Services.AddSingleton(new SqliteOptions(sqlitePath));
builder.Services.AddSingleton(new RuntimeDbOptions(runtimePath));
builder.Services.AddSingleton(new OperationalDbOptions(operationalConn));

var settings = AppSettingsFactory.Create(RuntimeProfile.ALTO, modelPath);
builder.Services.AddSingleton(settings);

// ---------------------------------------------------------
// 3. IA Y CONFIGURACIÓN DE INFERENCIA
// ---------------------------------------------------------
// Nota: el perfil dinámico ya tiene infraestructura y tabla,
// pero por ahora el LlmClient sigue usando configuración fija
// validada para esta máquina.
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

// Stores y repositorios
builder.Services.AddSingleton<ISchemaStore, SqliteSchemaStore>();
builder.Services.AddSingleton<ITrainingStore, SqliteTrainingStore>();
builder.Services.AddSingleton<IReviewStore, SqliteReviewStore>();
builder.Services.AddSingleton<IDocChunkRepository, SqliteDocChunkRepository>();
builder.Services.AddTransient<IJobStore, SqliteJobStore>();
builder.Services.AddSingleton<IBusinessRuleStore, SqliteBusinessRuleStore>();
builder.Services.AddSingleton<ISqlCacheService, SqlCacheService>();

// Pattern-first
builder.Services.AddSingleton<IPatternMatcherService, PatternMatcherService>();
builder.Services.AddSingleton<ITemplateSqlBuilder, TemplateSqlBuilder>();

// Retrieval, seguridad y ejecución
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
// 7. INICIALIZACIÓN DE BASES DE DATOS LOCALES
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
// PREPARACIÓN DEL ENTORNO DE RUNTIME (SQLite)
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
        -- LlmRuntimeProfile: perfiles de inferencia
        -- Nota: la infraestructura ya existe, pero aún no se
        -- conecta dinámicamente al LlmClient en runtime.
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
        -- QueryPatterns: preparado para pattern-first
        -- =====================================================
        CREATE TABLE IF NOT EXISTS QueryPatterns (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            PatternKey TEXT NOT NULL,
            IntentName TEXT NOT NULL,
            Description TEXT NULL,
            Tags TEXT NULL,
            Priority INTEGER NOT NULL DEFAULT 100,
            IsActive INTEGER NOT NULL DEFAULT 1,
            CreatedUtc TEXT NOT NULL
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
