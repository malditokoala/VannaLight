using Dapper;
using Microsoft.Data.Sqlite;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Infrastructure.Data;

public sealed class SqliteReviewStore : IReviewStore
{
    private static readonly SemaphoreSlim _writeLock = new(1, 1);

    // busy_timeout aquí hace efecto inmediatamente al abrir conexión
    private static string GetConnectionString(string path) =>
        $"Data Source={path};Mode=ReadWriteCreate;Cache=Shared;Default Timeout=30;";

    // ⚠️ Solo PRAGMAs “seguros” por conexión (NO journal_mode aquí)
    private static async Task ConfigurePerConnectionAsync(SqliteConnection connection, CancellationToken ct)
    {
        // Espera hasta 30s si está ocupada
        await connection.ExecuteAsync(new CommandDefinition("PRAGMA busy_timeout=30000;", cancellationToken: ct));
    }

    // ✅ PRAGMAs “de archivo” se ponen SOLO una vez (en Initialize)
    private static async Task ConfigureDatabaseOnceAsync(SqliteConnection connection, CancellationToken ct)
    {
        // WAL: permite lectores mientras hay escritor
        await connection.ExecuteAsync(new CommandDefinition("PRAGMA journal_mode=WAL;", cancellationToken: ct));

        // NORMAL recomendado con WAL
        await connection.ExecuteAsync(new CommandDefinition("PRAGMA synchronous=NORMAL;", cancellationToken: ct));

        // También setear busy_timeout aquí no estorba
        await connection.ExecuteAsync(new CommandDefinition("PRAGMA busy_timeout=30000;", cancellationToken: ct));
    }

    public async Task InitializeAsync(string sqlitePath, CancellationToken ct)
    {
        using var connection = new SqliteConnection(GetConnectionString(sqlitePath));
        await connection.OpenAsync(ct);

        await ConfigureDatabaseOnceAsync(connection, ct);

        var sql = @"
CREATE TABLE IF NOT EXISTS ReviewQueue (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Question TEXT NOT NULL,
    GeneratedSql TEXT NOT NULL,
    ErrorMessage TEXT,
    Status TEXT NOT NULL,
    Reason TEXT NOT NULL,
    CreatedUtc DATETIME NOT NULL
);";

        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct));
    }

    public async Task<long> EnqueueAsync(
        string sqlitePath,
        string question,
        string generatedSql,
        string? errorMessage,
        string reason,
        CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct);
        try
        {
            using var connection = new SqliteConnection(GetConnectionString(sqlitePath));
            await connection.OpenAsync(ct);

            // Solo PRAGMAs seguros por conexión
            await ConfigurePerConnectionAsync(connection, ct);

            var query = @"
INSERT INTO ReviewQueue (Question, GeneratedSql, ErrorMessage, Status, Reason, CreatedUtc) 
VALUES (@Question, @GeneratedSql, @ErrorMessage, 'Pending', @Reason, @Now)
RETURNING Id;";

            return await connection.ExecuteScalarAsync<long>(new CommandDefinition(query, new
            {
                Question = question,
                GeneratedSql = generatedSql,
                ErrorMessage = errorMessage,
                Reason = reason,
                Now = DateTime.UtcNow
            }, cancellationToken: ct));
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<IReadOnlyList<ReviewItem>> GetPendingReviewsAsync(string sqlitePath, CancellationToken ct)
    {
        using var connection = new SqliteConnection(GetConnectionString(sqlitePath));
        await connection.OpenAsync(ct);

        await ConfigurePerConnectionAsync(connection, ct);

        var result = await connection.QueryAsync<ReviewItem>(new CommandDefinition(
            "SELECT * FROM ReviewQueue WHERE Status = 'Pending' ORDER BY CreatedUtc ASC",
            cancellationToken: ct));

        return result.ToList();
    }

    public async Task<ReviewItem?> GetReviewByIdAsync(string sqlitePath, long id, CancellationToken ct)
    {
        using var connection = new SqliteConnection(GetConnectionString(sqlitePath));
        await connection.OpenAsync(ct);

        await ConfigurePerConnectionAsync(connection, ct);

        return await connection.QuerySingleOrDefaultAsync<ReviewItem>(new CommandDefinition(
            "SELECT * FROM ReviewQueue WHERE Id = @Id",
            new { Id = id },
            cancellationToken: ct));
    }

    public async Task UpdateReviewStatusAsync(string sqlitePath, long id, string status, CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct);
        try
        {
            using var connection = new SqliteConnection(GetConnectionString(sqlitePath));
            await connection.OpenAsync(ct);

            await ConfigurePerConnectionAsync(connection, ct);

            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE ReviewQueue SET Status = @Status WHERE Id = @Id",
                new { Status = status, Id = id },
                cancellationToken: ct));
        }
        finally
        {
            _writeLock.Release();
        }
    }
}