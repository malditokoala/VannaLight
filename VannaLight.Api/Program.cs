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

// 1. Cargar configuración de rutas
string operationalConn = builder.Configuration.GetConnectionString("OperationalDb") ?? "Server=localhost,1433;Database=Northwind;User Id=sa;Password=Chopsuey00;TrustServerCertificate=True;";
string sqlitePath = builder.Configuration["Paths:Sqlite"] ?? "vanna_memory.db";
string modelPath = builder.Configuration["Paths:Model"] ?? @"C:\Modelos\qwen2.5-coder-7b-instruct-q4_k_m.gguf";

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

// 2. Registrar Dependencias del Core e Infraestructura
// Usamos el Factory original de tu Fase 1
var settings = AppSettingsFactory.Create(RuntimeProfile.ALTO, modelPath);
builder.Services.AddSingleton(settings);

// --- REEMPLAZA LAS LÍNEAS ANTERIORES POR ESTAS ---
builder.Services.AddSingleton<ISchemaStore, SqliteSchemaStore>();
builder.Services.AddSingleton<ITrainingStore, SqliteTrainingStore>(); // ¡Aquí estaba la diferencia!
builder.Services.AddSingleton<IReviewStore, SqliteReviewStore>();
// -------------------------------------------------

builder.Services.AddSingleton<IRetriever, LocalRetriever>();
builder.Services.AddSingleton<ISqlValidator, StaticSqlValidator>();
builder.Services.AddSingleton<ISqlDryRunner, SqlServerDryRunner>();
builder.Services.AddSingleton<ILlmClient, LlmClient>(); // Singleton crucial para evitar OOM
builder.Services.AddTransient<AskUseCase>();

// 3. Registrar Dependencias de la API y el Worker
builder.Services.AddSingleton<IAskRequestQueue, AskRequestQueue>();
builder.Services.AddTransient<IJobStore, SqlServerJobStore>();

// EL WORKER: El guardaespaldas de tu GPU
builder.Services.AddHostedService<InferenceWorker>();

var app = builder.Build();

// 4. INICIALIZACIÓN AUTOMÁTICA DE LA BASE DE DATOS
await EnsureDatabaseSetupAsync(app.Services, operationalConn);

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

// --- Método de auto-creación de la tabla operacional ---
static async Task EnsureDatabaseSetupAsync(IServiceProvider services, string connectionString)
{
    using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    string sql = @"
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