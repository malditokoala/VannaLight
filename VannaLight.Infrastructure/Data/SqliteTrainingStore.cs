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

        const string sql = @"
            UPDATE TrainingExamples
            SET Sql = @Sql,
                IntentName = COALESCE(@IntentName, IntentName),
                IsVerified = CASE WHEN @IsVerified = 1 THEN 1 ELSE IsVerified END,
                Priority = CASE WHEN @Priority > Priority THEN @Priority ELSE Priority END,
                LastUsedUtc = @Now
            WHERE Question = @Question
              AND TenantKey = @TenantKey
              AND Domain = @Domain
              AND ConnectionName = @ConnectionName;

            INSERT INTO TrainingExamples
                (Question, Sql, TenantKey, Domain, ConnectionName, IntentName, IsVerified, Priority, CreatedUtc, LastUsedUtc, UseCount)
            SELECT
                @Question, @Sql, @TenantKey, @Domain, @ConnectionName, @IntentName, @IsVerified, @Priority, @Now, @Now, 0
            WHERE NOT EXISTS (
                SELECT 1
                FROM TrainingExamples
                WHERE Question = @Question
                  AND TenantKey = @TenantKey
                  AND Domain = @Domain
                  AND ConnectionName = @ConnectionName
            );";

        await conn.ExecuteAsync(
            new CommandDefinition(
                sql,
                new
                {
                    Question = NormalizeText(example.Question),
                    Sql = example.Sql,
                    TenantKey = NormalizeText(example.TenantKey),
                    Domain = NormalizeText(example.Domain),
                    ConnectionName = NormalizeText(example.ConnectionName),
                    IntentName = NormalizeNullableText(example.IntentName),
                    IsVerified = example.IsVerified ? 1 : 0,
                    example.Priority,
                    Now = DateTime.UtcNow
                },
                cancellationToken: ct));
    }

    public async Task InitializeAsync(string sqlitePath, CancellationToken ct)
    {
        await using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        await connection.OpenAsync(ct);
        await EnsureSchemaAsync(connection, ct);
    }

    public async Task<long> InsertTrainingExampleAsync(string sqlitePath, string question, string sql, CancellationToken ct)
    {
        await using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        await connection.OpenAsync(ct);
        await EnsureSchemaAsync(connection, ct);

        const string query = @"
            INSERT INTO TrainingExamples
                (Question, Sql, TenantKey, Domain, ConnectionName, IntentName, IsVerified, Priority, CreatedUtc, LastUsedUtc, UseCount)
            VALUES
                (@Question, @Sql, '', '', '', NULL, 0, 0, @Now, @Now, 1)
            RETURNING Id;";

        var now = DateTime.UtcNow;
        return await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                query,
                new
                {
                    Question = NormalizeText(question),
                    Sql = sql,
                    Now = now
                },
                cancellationToken: ct));
    }

    public async Task TouchExampleAsync(string sqlitePath, long id, CancellationToken ct)
    {
        await using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        await connection.OpenAsync(ct);
        await EnsureSchemaAsync(connection, ct);

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE TrainingExamples SET LastUsedUtc = @Now, UseCount = UseCount + 1 WHERE Id = @Id",
                new { Now = DateTime.UtcNow, Id = id },
                cancellationToken: ct));
    }

    public async Task<IReadOnlyList<TrainingExample>> GetAllTrainingExamplesAsync(string sqlitePath, CancellationToken ct)
    {
        await using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        await connection.OpenAsync(ct);
        await EnsureSchemaAsync(connection, ct);

        const string query = @"
            SELECT
                Id,
                Question,
                Sql,
                TenantKey,
                NULLIF(Domain, '') AS Domain,
                ConnectionName,
                IntentName,
                IsVerified,
                Priority,
                CreatedUtc,
                LastUsedUtc,
                COALESCE(UseCount, 0) AS UseCount
            FROM TrainingExamples;";

        var result = await connection.QueryAsync<TrainingExample>(
            new CommandDefinition(query, cancellationToken: ct));
        return result.ToList();
    }

    private static async Task EnsureSchemaAsync(SqliteConnection connection, CancellationToken ct)
    {
        var columns = (await connection.QueryAsync<string>(
            new CommandDefinition("SELECT name FROM pragma_table_info('TrainingExamples');", cancellationToken: ct)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (columns.Count == 0)
        {
            const string createTableSql = @"
                CREATE TABLE IF NOT EXISTS TrainingExamples (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Question TEXT NOT NULL,
                    Sql TEXT NOT NULL,
                    TenantKey TEXT NOT NULL DEFAULT '',
                    Domain TEXT NOT NULL DEFAULT '',
                    ConnectionName TEXT NOT NULL DEFAULT '',
                    IntentName TEXT NULL,
                    IsVerified INTEGER NOT NULL DEFAULT 0,
                    Priority INTEGER NOT NULL DEFAULT 0,
                    CreatedUtc DATETIME NOT NULL,
                    LastUsedUtc DATETIME NOT NULL,
                    UseCount INTEGER NOT NULL DEFAULT 0
                );";

            await connection.ExecuteAsync(new CommandDefinition(createTableSql, cancellationToken: ct));
        }
        else
        {
            var needsMigration =
                !columns.Contains("TenantKey") ||
            !columns.Contains("ConnectionName") ||
            await HasLegacyQuestionUniqueConstraintAsync(connection, ct);

            if (needsMigration)
            {
                await MigrateSchemaAsync(connection, ct);
            }
        }

        const string indexSql = @"
            CREATE UNIQUE INDEX IF NOT EXISTS IX_TrainingExamples_ContextQuestion
                ON TrainingExamples(Question, TenantKey, Domain, ConnectionName);

            CREATE INDEX IF NOT EXISTS IX_TrainingExamples_ContextIntentVerified
                ON TrainingExamples(TenantKey, Domain, ConnectionName, IntentName, IsVerified, Priority DESC);";

        await connection.ExecuteAsync(new CommandDefinition(indexSql, cancellationToken: ct));
    }

    private static async Task<bool> HasLegacyQuestionUniqueConstraintAsync(SqliteConnection connection, CancellationToken ct)
    {
        const string sql = @"
            SELECT sql
            FROM sqlite_master
            WHERE type = 'table'
              AND name = 'TrainingExamples';";

        var createSql = await connection.ExecuteScalarAsync<string?>(
            new CommandDefinition(sql, cancellationToken: ct));

        if (!string.IsNullOrWhiteSpace(createSql) &&
            createSql.Contains("Question TEXT NOT NULL UNIQUE", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        const string indexSql = @"
            SELECT name
            FROM pragma_index_list('TrainingExamples')
            WHERE [unique] = 1;";

        var uniqueIndexes = (await connection.QueryAsync<string>(
            new CommandDefinition(indexSql, cancellationToken: ct)))
            .ToList();

        return uniqueIndexes.Any(name =>
            string.Equals(name, "IX_TrainingExamples_Question", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task MigrateSchemaAsync(SqliteConnection connection, CancellationToken ct)
    {
        const string migrationSql = @"
            BEGIN IMMEDIATE TRANSACTION;

            DROP TABLE IF EXISTS TrainingExamples_new;

            CREATE TABLE TrainingExamples_new (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Question TEXT NOT NULL,
                Sql TEXT NOT NULL,
                TenantKey TEXT NOT NULL DEFAULT '',
                Domain TEXT NOT NULL DEFAULT '',
                ConnectionName TEXT NOT NULL DEFAULT '',
                IntentName TEXT NULL,
                IsVerified INTEGER NOT NULL DEFAULT 0,
                Priority INTEGER NOT NULL DEFAULT 0,
                CreatedUtc DATETIME NOT NULL,
                LastUsedUtc DATETIME NOT NULL,
                UseCount INTEGER NOT NULL DEFAULT 0
            );

            INSERT INTO TrainingExamples_new
                (Id, Question, Sql, TenantKey, Domain, ConnectionName, IntentName, IsVerified, Priority, CreatedUtc, LastUsedUtc, UseCount)
            SELECT
                Id,
                Question,
                Sql,
                COALESCE(TenantKey, ''),
                COALESCE(Domain, ''),
                COALESCE(ConnectionName, ''),
                IntentName,
                COALESCE(IsVerified, 0),
                COALESCE(Priority, 0),
                COALESCE(CreatedUtc, CURRENT_TIMESTAMP),
                COALESCE(LastUsedUtc, CURRENT_TIMESTAMP),
                COALESCE(UseCount, 0)
            FROM TrainingExamples;

            DROP TABLE IF EXISTS TrainingExamples;
            ALTER TABLE TrainingExamples_new RENAME TO TrainingExamples;

            COMMIT;";

        await connection.ExecuteAsync(new CommandDefinition(migrationSql, cancellationToken: ct));
    }

    private static string NormalizeText(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string? NormalizeNullableText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
