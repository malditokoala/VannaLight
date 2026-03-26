using VannaLight.Core.Models;

namespace VannaLight.Core.Abstractions;

// Interfaz para extraer el esquema directamente desde SQL Server
public interface ISchemaIngestor
{
    Task<IReadOnlyList<TableSchema>> ReadSchemaAsync(string sqlServerConnectionString, CancellationToken ct);
}

// Interfaz para la persistencia del esquema (ej. SQLite)
public interface ISchemaStore
{
    Task InitializeAsync(string sqlitePath, CancellationToken ct);
    Task UpsertSchemaDocsAsync(string sqlitePath, IReadOnlyList<TableSchemaDoc> docs, CancellationToken ct);
    Task<IReadOnlyList<TableSchemaDoc>> GetAllSchemaDocsAsync(string sqlitePath, CancellationToken ct);
}

// Interfaz para la persistencia de ejemplos de entrenamiento (memoria validada)
public interface ITrainingStore
{
    Task InitializeAsync(string sqlitePath, CancellationToken ct);
    Task<long> InsertTrainingExampleAsync(string sqlitePath, string question, string sql, CancellationToken ct);
    Task TouchExampleAsync(string sqlitePath, long id, CancellationToken ct);
    Task<IReadOnlyList<TrainingExample>> GetAllTrainingExamplesAsync(string sqlitePath, CancellationToken ct);
    Task UpsertAsync(TrainingExampleUpsert example, CancellationToken ct);

}

// Interfaz para el motor RAG Híbrido
public interface IRetriever
{
    Task<RetrievalContext> RetrieveAsync(string sqlitePath, string question, string domain, string? intentName, CancellationToken ct);
}

// Interfaz para el LLM Local (LLamaSharp)
public interface ILlmClient
{
    Task<string> GenerateSqlAsync(string prompt, CancellationToken ct);
    Task<string> CompleteAsync(string prompt, CancellationToken ct);
}

// Interfaz para las reglas de seguridad estáticas
public interface ISqlValidator
{
    // Valida seguridad y formato (SELECT-only, 1 statement, sin peligrosos, sin SELECT *)
    bool TryValidate(string sql, out string error);
}

// Interfaz para probar la compilación sin afectar los datos
public interface ISqlDryRunner
{
    // Opcional: compilar sin ejecutar (NOEXEC ON) contra SQL Server.
    Task<(bool Ok, string? Error)> DryRunAsync(string sqlServerConnectionString, string sql, CancellationToken ct);
}
// Interfaz para gestionar la Cola de Revisión (ReviewQueue)
public interface IReviewStore
{
    Task InitializeAsync(string sqlitePath, CancellationToken ct);
    Task<long> EnqueueAsync(string sqlitePath, string question, string generatedSql, string? errorMessage, string reason, CancellationToken ct);
    Task<IReadOnlyList<ReviewItem>> GetPendingReviewsAsync(string sqlitePath, CancellationToken ct);
    Task<ReviewItem?> GetReviewByIdAsync(string sqlitePath, long id, CancellationToken ct);
    Task UpdateReviewStatusAsync(string sqlitePath, long id, string status, CancellationToken ct);
}
