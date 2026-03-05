using Dapper;
using Microsoft.Data.SqlClient;
using VannaLight.Api.Data;
using VannaLight.Api.Hubs;
using VannaLight.Api.Services;
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

// 1) Resolve paths (ContentRoot) — estable entre PCs + publish
var sqliteRel = builder.Configuration["Paths:Sqlite"] ?? "Data/vanna_memory.db";
var sqlitePath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, sqliteRel));
Directory.CreateDirectory(Path.GetDirectoryName(sqlitePath)!);


var modelRelOrAbs = builder.Configuration["Paths:Model"] ?? @"C:\Modelos\qwen2.5-coder-7b-instruct-q4_k_m.gguf";
var modelPath = Path.IsPathRooted(modelRelOrAbs)
	? modelRelOrAbs
	: Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, modelRelOrAbs));

// 2) Connection string MUST exist (no defaults con password)
var operationalConn = builder.Configuration.GetConnectionString("OperationalDb")
	?? throw new InvalidOperationException("Falta ConnectionStrings:OperationalDb (usa appsettings.{env}.json o user-secrets).");

// 3) Options strongly-typed (en Core.Settings)
builder.Services.AddSingleton(new SqliteOptions(sqlitePath));
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
builder.Services.AddTransient<TrainExampleUseCase>(); // ✅ agregado

// 6) Stores / Infra
builder.Services.AddSingleton<ISchemaStore, SqliteSchemaStore>();
builder.Services.AddSingleton<ITrainingStore, SqliteTrainingStore>();
builder.Services.AddSingleton<IReviewStore, SqliteReviewStore>();

builder.Services.AddSingleton<IRetriever, LocalRetriever>();
builder.Services.AddSingleton<ISqlValidator, StaticSqlValidator>();
builder.Services.AddSingleton<ISqlDryRunner, SqlServerDryRunner>();

builder.Services.AddSingleton<ILlmClient, LlmClient>(); // ok como singleton si es thread-safe

// 7) Docs
builder.Services.AddSingleton<WiDocIngestor>();
builder.Services.AddSingleton<DocsAnswerService>();

// 8) Worker + API dependencies
builder.Services.AddSingleton<IAskRequestQueue, AskRequestQueue>();
builder.Services.AddTransient<IJobStore, SqlServerJobStore>();
builder.Services.AddHostedService<InferenceWorker>();

var app = builder.Build();

// 9) DB setup operational
await EnsureDatabaseSetupAsync(app.Services);

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

static async Task EnsureDatabaseSetupAsync(IServiceProvider services)
{
	var op = services.GetRequiredService<OperationalDbOptions>();

	using var connection = new SqlConnection(op.ConnectionString);
	await connection.OpenAsync();

	const string sql = @"
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[QuestionJobs]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.QuestionJobs (
        JobId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        UserId NVARCHAR(100) NOT NULL,
        [Role] NVARCHAR(50) NOT NULL,
        Question NVARCHAR(MAX) NOT NULL,
        [Status] NVARCHAR(30) NOT NULL,
        CreatedUtc DATETIME2 NOT NULL,
        UpdatedUtc DATETIME2 NOT NULL,
        SqlText NVARCHAR(MAX) NULL,
        ErrorText NVARCHAR(MAX) NULL,
        ResultJson NVARCHAR(MAX) NULL,
        Attempt INT NOT NULL DEFAULT 0,
        TrainingExampleSaved BIT NOT NULL DEFAULT 0 
    );

    CREATE INDEX IX_QuestionJobs_User_Created ON dbo.QuestionJobs(UserId, CreatedUtc DESC);
    CREATE INDEX IX_QuestionJobs_Status ON dbo.QuestionJobs([Status], UpdatedUtc DESC);
END";
	await connection.ExecuteAsync(sql);
	Console.WriteLine("[DB Setup] Tabla QuestionJobs verificada/creada exitosamente.");
}