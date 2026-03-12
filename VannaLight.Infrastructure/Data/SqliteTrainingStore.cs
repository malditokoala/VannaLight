using Dapper;
using Microsoft.Data.Sqlite;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;
using Microsoft.Data.Sqlite;
using VannaLight.Core.Settings;

namespace VannaLight.Infrastructure.Data;

public class SqliteTrainingStore : ITrainingStore
{
    private readonly SqliteOptions _opt;

    public SqliteTrainingStore(SqliteOptions opt)
        => _opt = opt;

    public async Task UpsertByQuestionAsync(string question, string sql, CancellationToken ct)
    {
        await using var conn = new SqliteConnection($"Data Source={_opt.DbPath}");
        await conn.OpenAsync(ct);

        // 🚀 BYPASS DE SQLITE: Usamos UPDATE y luego INSERT condicionado.
        // Esto evita el error de ON CONFLICT si la tabla se creó originalmente sin el UNIQUE.
        const string q = @"
            UPDATE TrainingExamples 
            SET Sql = @Sql, LastUsedUtc = @Now 
            WHERE Question = @Question;

            INSERT INTO TrainingExamples (Question, Sql, CreatedUtc, LastUsedUtc)
            SELECT @Question, @Sql, @Now, @Now
            WHERE NOT EXISTS (SELECT 1 FROM TrainingExamples WHERE Question = @Question);";

        await conn.ExecuteAsync(q, new { Question = question, Sql = sql, Now = DateTime.UtcNow });
    }

    public async Task InitializeAsync(string sqlitePath, CancellationToken ct)
    {
        using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        await connection.OpenAsync(ct);

        // Mantenemos esto para futuras creaciones desde cero
        var createTableSql = @"
            CREATE TABLE IF NOT EXISTS TrainingExamples (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Question TEXT NOT NULL UNIQUE,
                Sql TEXT NOT NULL,
                CreatedUtc DATETIME NOT NULL,
                LastUsedUtc DATETIME NOT NULL,
                UseCount INTEGER DEFAULT 0
            );
            
            CREATE UNIQUE INDEX IF NOT EXISTS IX_TrainingExamples_Question ON TrainingExamples(Question);";

        await connection.ExecuteAsync(createTableSql);
    }

    public async Task<long> InsertTrainingExampleAsync(string sqlitePath, string question, string sql, CancellationToken ct)
    {
        using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        var query = @"
            INSERT INTO TrainingExamples (Question, Sql, CreatedUtc, LastUsedUtc, UseCount) 
            VALUES (@Question, @Sql, @Now, @Now, 1)
            RETURNING Id;";

        var now = DateTime.UtcNow;
        return await connection.ExecuteScalarAsync<long>(query, new { Question = question, Sql = sql, Now = now });
    }

    public async Task TouchExampleAsync(string sqlitePath, long id, CancellationToken ct)
    {
        using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        await connection.ExecuteAsync(
            "UPDATE TrainingExamples SET LastUsedUtc = @Now, UseCount = UseCount + 1 WHERE Id = @Id",
            new { Now = DateTime.UtcNow, Id = id });
    }

    public async Task<IReadOnlyList<TrainingExample>> GetAllTrainingExamplesAsync(string sqlitePath, CancellationToken ct)
    {
        using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        var result = await connection.QueryAsync<TrainingExample>("SELECT * FROM TrainingExamples");
        return result.ToList();
    }
}