using Dapper;
using Microsoft.Data.SqlClient;
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

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

// 1) Resolve paths (ContentRoot)
var sqliteRel = builder.Configuration["Paths:Sqlite"] ?? "Data/vanna_memory.db";
var sqlitePath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, sqliteRel));
Directory.CreateDirectory(Path.GetDirectoryName(sqlitePath)!);

// NUEVO: Ruta para la base de datos de Runtime (Estado/Jobs)
var runtimeRel = builder.Configuration["Paths:RuntimeDb"] ?? "Data/vanna_runtime.db";
var runtimePath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, runtimeRel));
Directory.CreateDirectory(Path.GetDirectoryName(runtimePath)!);

var modelRelOrAbs = builder.Configuration["Paths:Model"] ?? @"C:\Modelos\qwen2.5-coder-7b-instruct-q4_k_m.gguf";
var modelPath = Path.IsPathRooted(modelRelOrAbs)
    ? modelRelOrAbs
    : Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, modelRelOrAbs));

// 2) Connection string MUST exist (no defaults con password)
var operationalConn = builder.Configuration.GetConnectionString("OperationalDb")
    ?? throw new InvalidOperationException("Falta ConnectionStrings:OperationalDb (usa appsettings.{env}.json o user-secrets).");

// 3) Options strongly-typed
builder.Services.AddSingleton(new SqliteOptions(sqlitePath));
builder.Services.AddSingleton(new RuntimeDbOptions(runtimePath)); // Opciones para el Runtime
builder.Services.AddSingleton(new OperationalDbOptions(operationalConn));

// Controllers / infra
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
builder.Services.AddMemoryCache();

// 4) Core settings
var settings = AppSettingsFactory.Create(RuntimeProfile.ALTO, modelPath);
builder.Services.AddSingleton(settings);

// 5) Core UseCases
builder.Services.AddTransient<AskUseCase>();
builder.Services.AddTransient<TrainExampleUseCase>();

// 6) Stores / Infra (Conocimiento va a Memory)
builder.Services.AddSingleton<ISchemaStore, SqliteSchemaStore>();
builder.Services.AddSingleton<ITrainingStore, SqliteTrainingStore>();
builder.Services.AddSingleton<IReviewStore, SqliteReviewStore>();

builder.Services.AddSingleton<IRetriever, LocalRetriever>();
builder.Services.AddSingleton<ISqlValidator, StaticSqlValidator>();
builder.Services.AddSingleton<ISqlDryRunner, SqlServerDryRunner>();
builder.Services.AddSingleton<ILlmClient, LlmClient>();

// 7) Docs
builder.Services.AddSingleton<WiDocIngestor>();
builder.Services.AddSingleton<IDocsIntentRouter, DocsIntentRouterLlm>();
builder.Services.AddSingleton<IDocsAnswerService, DocsAnswerService>();

// 8) Worker + API dependencies
builder.Services.AddSingleton<IAskRequestQueue, AskRequestQueue>();
builder.Services.AddSingleton<IDocChunkRepository, SqliteDocChunkRepository>();

// CAMBIO ARQUITECTÓNICO CRÍTICO: Usamos SQLite para los Jobs, protegiendo Producción
builder.Services.AddTransient<IJobStore, SqliteJobStore>();
builder.Services.AddHostedService<InferenceWorker>();

// ML.NET
builder.Services.AddSingleton<IPredictionIntentRouter, PredictionIntentRouterLlm>();
builder.Services.AddSingleton<IForecastingService, ForecastingService>();
builder.Services.AddSingleton<IPredictionAnswerService, PredictionAnswerService>();

var app = builder.Build();

// 9) DB setup local (Runtime) - Cero impacto en el ERP
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

// NUEVA FUNCIÓN: Solo inicializa la BD de SQLite local
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
            Question TEXT NOT NULL,
            Status TEXT NOT NULL,
            CreatedUtc TEXT NOT NULL,
            UpdatedUtc TEXT NOT NULL,
            SqlText TEXT,
            ErrorText TEXT,
            ResultJson TEXT,
            Attempt INTEGER NOT NULL DEFAULT 0,
            TrainingExampleSaved INTEGER NOT NULL DEFAULT 0 
        );
        CREATE INDEX IF NOT EXISTS IX_QuestionJobs_User_Created ON QuestionJobs(UserId, CreatedUtc DESC);
        CREATE INDEX IF NOT EXISTS IX_QuestionJobs_Status ON QuestionJobs(Status, UpdatedUtc DESC);
    ";

    await connection.ExecuteAsync(sql);
    Console.WriteLine($"[DB Setup] Tabla QuestionJobs verificada/creada en SQLite Runtime ({options.DbPath}).");
}