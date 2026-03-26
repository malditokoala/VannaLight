using Dapper;
using Microsoft.Data.Sqlite;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;
using VannaLight.Core.Settings;

namespace VannaLight.Infrastructure.Data;

public class SqliteTrainingStore : ITrainingStore
{
    private readonly SqliteOptions _opt;

    public SqliteTrainingStore(SqliteOptions opt)
        => _opt = opt;

    public async Task UpsertAsync(TrainingExampleUpsert example, CancellationToken ct)
    {
        await using var conn = new SqliteConnection($"Data Source={_opt.DbPath}");
        await conn.OpenAsync(ct);
        await EnsureSchemaAsync(conn, ct);

        const string q = @"
            UPDATE TrainingExamples 
            SET Sql = @Sql,
                Domain = COALESCE(@Domain, Domain),
                IntentName = COALESCE(@IntentName, IntentName),
                IsVerified = CASE WHEN @IsVerified = 1 THEN 1 ELSE IsVerified END,
                Priority = CASE WHEN @Priority > Priority THEN @Priority ELSE Priority END,
                LastUsedUtc = @Now
            WHERE Question = @Question;

            INSERT INTO TrainingExamples (Question, Sql, Domain, IntentName, IsVerified, Priority, CreatedUtc, LastUsedUtc)
            SELECT @Question, @Sql, @Domain, @IntentName, @IsVerified, @Priority, @Now, @Now
            WHERE NOT EXISTS (SELECT 1 FROM TrainingExamples WHERE Question = @Question);";

        await conn.ExecuteAsync(q, new
        {
            example.Question,
            example.Sql,
            example.Domain,
            example.IntentName,
            IsVerified = example.IsVerified ? 1 : 0,
            example.Priority,
            Now = DateTime.UtcNow
        });
    }

    public async Task InitializeAsync(string sqlitePath, CancellationToken ct)
    {
        using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        await connection.OpenAsync(ct);
        await EnsureSchemaAsync(connection, ct);
    }

    public async Task<long> InsertTrainingExampleAsync(string sqlitePath, string question, string sql, CancellationToken ct)
    {
        using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        await connection.OpenAsync(ct);
        await EnsureSchemaAsync(connection, ct);
        var query = @"
            INSERT INTO TrainingExamples (Question, Sql, Domain, IntentName, IsVerified, Priority, CreatedUtc, LastUsedUtc, UseCount) 
            VALUES (@Question, @Sql, NULL, NULL, 0, 0, @Now, @Now, 1)
            RETURNING Id;";

        var now = DateTime.UtcNow;
        return await connection.ExecuteScalarAsync<long>(query, new { Question = question, Sql = sql, Now = now });
    }

    public async Task TouchExampleAsync(string sqlitePath, long id, CancellationToken ct)
    {
        using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        await connection.OpenAsync(ct);
        await EnsureSchemaAsync(connection, ct);
        await connection.ExecuteAsync(
            "UPDATE TrainingExamples SET LastUsedUtc = @Now, UseCount = UseCount + 1 WHERE Id = @Id",
            new { Now = DateTime.UtcNow, Id = id });
    }

    public async Task<IReadOnlyList<TrainingExample>> GetAllTrainingExamplesAsync(string sqlitePath, CancellationToken ct)
    {
        using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        await connection.OpenAsync(ct);
        await EnsureSchemaAsync(connection, ct);
        var result = await connection.QueryAsync<TrainingExample>("SELECT * FROM TrainingExamples");
        return result.ToList();
    }

    private static async Task EnsureSchemaAsync(SqliteConnection connection, CancellationToken ct)
    {
        var createTableSql = @"
            CREATE TABLE IF NOT EXISTS TrainingExamples (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Question TEXT NOT NULL UNIQUE,
                Sql TEXT NOT NULL,
                Domain TEXT NULL,
                IntentName TEXT NULL,
                IsVerified INTEGER NOT NULL DEFAULT 0,
                Priority INTEGER NOT NULL DEFAULT 0,
                CreatedUtc DATETIME NOT NULL,
                LastUsedUtc DATETIME NOT NULL,
                UseCount INTEGER DEFAULT 0
            );

            CREATE UNIQUE INDEX IF NOT EXISTS IX_TrainingExamples_Question ON TrainingExamples(Question);
            CREATE INDEX IF NOT EXISTS IX_TrainingExamples_Domain_Intent_Verified
                ON TrainingExamples(Domain, IntentName, IsVerified, Priority DESC);";

        await connection.ExecuteAsync(new CommandDefinition(createTableSql, cancellationToken: ct));

        var columns = (await connection.QueryAsync<string>(
            new CommandDefinition("SELECT name FROM pragma_table_info('TrainingExamples');", cancellationToken: ct)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!columns.Contains("Domain"))
            await connection.ExecuteAsync(new CommandDefinition("ALTER TABLE TrainingExamples ADD COLUMN Domain TEXT NULL;", cancellationToken: ct));

        if (!columns.Contains("IntentName"))
            await connection.ExecuteAsync(new CommandDefinition("ALTER TABLE TrainingExamples ADD COLUMN IntentName TEXT NULL;", cancellationToken: ct));

        if (!columns.Contains("IsVerified"))
            await connection.ExecuteAsync(new CommandDefinition("ALTER TABLE TrainingExamples ADD COLUMN IsVerified INTEGER NOT NULL DEFAULT 0;", cancellationToken: ct));

        if (!columns.Contains("Priority"))
            await connection.ExecuteAsync(new CommandDefinition("ALTER TABLE TrainingExamples ADD COLUMN Priority INTEGER NOT NULL DEFAULT 0;", cancellationToken: ct));
    }
}
