using Dapper;
using Microsoft.Data.Sqlite;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Infrastructure.Data;

public sealed class SqliteSemanticHintStore : ISemanticHintStore
{
    public async Task<IReadOnlyList<SemanticHint>> GetActiveHintsAsync(
        string sqlitePath,
        string domain,
        int maxHints,
        CancellationToken ct = default)
    {
        using var conn = new SqliteConnection($"Data Source={sqlitePath};");
        await conn.OpenAsync(ct);
        await EnsureSchemaAsync(conn, ct);

        const string sql = @"
SELECT
    Id,
    Domain,
    HintKey,
    HintType,
    DisplayName,
    ObjectName,
    ColumnName,
    HintText,
    Priority,
    IsActive
FROM SemanticHints
WHERE Domain = @Domain
  AND IsActive = 1
ORDER BY Priority ASC, Id ASC
LIMIT @MaxHints;";

        var rows = await conn.QueryAsync<SemanticHint>(
            new CommandDefinition(sql, new { Domain = domain, MaxHints = maxHints }, cancellationToken: ct));

        return rows.AsList();
    }

    public async Task<IReadOnlyList<SemanticHint>> GetAllHintsAsync(
        string sqlitePath,
        string domain,
        CancellationToken ct = default)
    {
        using var conn = new SqliteConnection($"Data Source={sqlitePath};");
        await conn.OpenAsync(ct);
        await EnsureSchemaAsync(conn, ct);

        const string sql = @"
SELECT
    Id,
    Domain,
    HintKey,
    HintType,
    DisplayName,
    ObjectName,
    ColumnName,
    HintText,
    Priority,
    IsActive
FROM SemanticHints
WHERE Domain = @Domain
ORDER BY IsActive DESC, Priority ASC, Id ASC;";

        var rows = await conn.QueryAsync<SemanticHint>(
            new CommandDefinition(sql, new { Domain = domain }, cancellationToken: ct));

        return rows.AsList();
    }

    public async Task<long> UpsertAsync(
        string sqlitePath,
        SemanticHint hint,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(hint);

        using var conn = new SqliteConnection($"Data Source={sqlitePath};");
        await conn.OpenAsync(ct);
        await EnsureSchemaAsync(conn, ct);

        var normalizedDomain = NormalizeRequired(hint.Domain, nameof(hint.Domain));
        var hintKey = NormalizeRequired(hint.HintKey, nameof(hint.HintKey));
        var hintType = NormalizeRequired(hint.HintType, nameof(hint.HintType));
        var hintText = NormalizeRequired(hint.HintText, nameof(hint.HintText));
        var displayName = NormalizeOptional(hint.DisplayName);
        var objectName = NormalizeOptional(hint.ObjectName);
        var columnName = NormalizeOptional(hint.ColumnName);

        if (hint.Id > 0)
        {
            const string updateSql = @"
UPDATE SemanticHints
SET
    Domain = @Domain,
    HintKey = @HintKey,
    HintType = @HintType,
    DisplayName = @DisplayName,
    ObjectName = @ObjectName,
    ColumnName = @ColumnName,
    HintText = @HintText,
    Priority = @Priority,
    IsActive = @IsActive,
    UpdatedUtc = @UpdatedUtc
WHERE Id = @Id;";

            var affected = await conn.ExecuteAsync(
                new CommandDefinition(
                    updateSql,
                    new
                    {
                        hint.Id,
                        Domain = normalizedDomain,
                        HintKey = hintKey,
                        HintType = hintType,
                        DisplayName = displayName,
                        ObjectName = objectName,
                        ColumnName = columnName,
                        HintText = hintText,
                        hint.Priority,
                        IsActive = hint.IsActive ? 1 : 0,
                        UpdatedUtc = DateTime.UtcNow
                    },
                    cancellationToken: ct));

            return affected > 0 ? hint.Id : 0;
        }

        const string insertSql = @"
INSERT INTO SemanticHints
(
    Domain,
    HintKey,
    HintType,
    DisplayName,
    ObjectName,
    ColumnName,
    HintText,
    Priority,
    IsActive,
    CreatedUtc,
    UpdatedUtc
)
VALUES
(
    @Domain,
    @HintKey,
    @HintType,
    @DisplayName,
    @ObjectName,
    @ColumnName,
    @HintText,
    @Priority,
    @IsActive,
    @CreatedUtc,
    @CreatedUtc
);
SELECT last_insert_rowid();";

        return await conn.ExecuteScalarAsync<long>(
            new CommandDefinition(
                insertSql,
                new
                {
                    Domain = normalizedDomain,
                    HintKey = hintKey,
                    HintType = hintType,
                    DisplayName = displayName,
                    ObjectName = objectName,
                    ColumnName = columnName,
                    HintText = hintText,
                    hint.Priority,
                    IsActive = hint.IsActive ? 1 : 0,
                    CreatedUtc = DateTime.UtcNow
                },
                cancellationToken: ct));
    }

    public async Task<bool> SetIsActiveAsync(
        string sqlitePath,
        long id,
        bool isActive,
        CancellationToken ct = default)
    {
        using var conn = new SqliteConnection($"Data Source={sqlitePath};");
        await conn.OpenAsync(ct);
        await EnsureSchemaAsync(conn, ct);

        const string sql = @"
UPDATE SemanticHints
SET
    IsActive = @IsActive,
    UpdatedUtc = @UpdatedUtc
WHERE Id = @Id;";

        var affected = await conn.ExecuteAsync(
            new CommandDefinition(
                sql,
                new
                {
                    Id = id,
                    IsActive = isActive ? 1 : 0,
                    UpdatedUtc = DateTime.UtcNow
                },
                cancellationToken: ct));

        return affected > 0;
    }

    private static string NormalizeRequired(string? value, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{argumentName} es requerido.", argumentName);

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static async Task EnsureSchemaAsync(SqliteConnection connection, CancellationToken ct)
    {
        const string sql = @"
CREATE TABLE IF NOT EXISTS SemanticHints (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Domain TEXT NOT NULL,
    HintKey TEXT NOT NULL,
    HintType TEXT NOT NULL,
    DisplayName TEXT NULL,
    ObjectName TEXT NULL,
    ColumnName TEXT NULL,
    HintText TEXT NOT NULL,
    Priority INTEGER NOT NULL DEFAULT 100,
    IsActive INTEGER NOT NULL DEFAULT 1,
    CreatedUtc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedUtc TEXT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS IX_SemanticHints_Domain_HintKey
    ON SemanticHints(Domain, HintKey);

CREATE INDEX IF NOT EXISTS IX_SemanticHints_Domain_IsActive_Priority
    ON SemanticHints(Domain, IsActive, Priority, Id);";

        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct));
    }
}
