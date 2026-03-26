using System.IO;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;
using VannaLight.Core.Settings;

namespace VannaLight.Infrastructure.Data;

public sealed class SqliteQueryPatternTermStore : IQueryPatternTermStore
{
    private readonly string _connectionString;

    public SqliteQueryPatternTermStore(IOptions<SqliteOptions> sqliteOptions)
    {
        ArgumentNullException.ThrowIfNull(sqliteOptions);
        ArgumentNullException.ThrowIfNull(sqliteOptions.Value);

        var dbPath = sqliteOptions.Value.DbPath;
        if (string.IsNullOrWhiteSpace(dbPath))
            throw new InvalidOperationException("SqliteOptions.DbPath no está configurado.");

        if (!Path.IsPathRooted(dbPath))
            dbPath = Path.GetFullPath(dbPath);

        _connectionString = $"Data Source={dbPath};Cache=Shared;";
    }

    public async Task<IReadOnlyList<QueryPatternTerm>> GetActiveByPatternIdsAsync(
        IReadOnlyCollection<long> patternIds,
        CancellationToken ct = default)
    {
        if (patternIds == null || patternIds.Count == 0)
            return Array.Empty<QueryPatternTerm>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        await EnsureSchemaAsync(connection, ct);

        const string sql = @"
SELECT
    Id,
    PatternId,
    Term,
    TermGroup,
    MatchMode,
    IsRequired,
    IsActive,
    CreatedUtc
FROM QueryPatternTerms
WHERE PatternId IN @PatternIds
  AND IsActive = 1
ORDER BY PatternId ASC, IsRequired DESC, Id ASC;";

        var rows = await connection.QueryAsync<QueryPatternTerm>(
            new CommandDefinition(sql, new { PatternIds = patternIds }, cancellationToken: ct));

        return rows.AsList();
    }

    public async Task<IReadOnlyList<QueryPatternTerm>> GetAllByPatternIdAsync(long patternId, CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        await EnsureSchemaAsync(connection, ct);

        const string sql = @"
SELECT
    Id,
    PatternId,
    Term,
    TermGroup,
    MatchMode,
    IsRequired,
    IsActive,
    CreatedUtc
FROM QueryPatternTerms
WHERE PatternId = @PatternId
ORDER BY IsActive DESC, IsRequired DESC, Id ASC;";

        var rows = await connection.QueryAsync<QueryPatternTerm>(
            new CommandDefinition(sql, new { PatternId = patternId }, cancellationToken: ct));

        return rows.AsList();
    }

    public async Task<long> UpsertAsync(QueryPatternTerm term, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(term);

        if (term.PatternId <= 0)
            throw new ArgumentException("PatternId es requerido.", nameof(term.PatternId));

        var normalizedTerm = NormalizeRequired(term.Term, nameof(term.Term));
        var normalizedGroup = NormalizeRequired(term.TermGroup, nameof(term.TermGroup));
        var normalizedMatchMode = NormalizeMatchMode(term.MatchMode);
        var now = DateTime.UtcNow;

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        await EnsureSchemaAsync(connection, ct);

        var existingId = term.Id > 0
            ? term.Id
            : await connection.ExecuteScalarAsync<long?>(
                new CommandDefinition(
                    @"
SELECT Id
FROM QueryPatternTerms
WHERE PatternId = @PatternId
  AND LOWER(TRIM(TermGroup)) = LOWER(TRIM(@TermGroup))
  AND LOWER(TRIM(Term)) = LOWER(TRIM(@Term))
LIMIT 1;",
                    new
                    {
                        term.PatternId,
                        TermGroup = normalizedGroup,
                        Term = normalizedTerm
                    },
                    cancellationToken: ct));

        if (existingId.HasValue && existingId.Value > 0)
        {
            const string updateSql = @"
UPDATE QueryPatternTerms
SET
    PatternId = @PatternId,
    Term = @Term,
    TermGroup = @TermGroup,
    MatchMode = @MatchMode,
    IsRequired = @IsRequired,
    IsActive = @IsActive
WHERE Id = @Id;";

            await connection.ExecuteAsync(
                new CommandDefinition(
                    updateSql,
                    new
                    {
                        Id = existingId.Value,
                        term.PatternId,
                        Term = normalizedTerm,
                        TermGroup = normalizedGroup,
                        MatchMode = normalizedMatchMode,
                        IsRequired = term.IsRequired ? 1 : 0,
                        IsActive = term.IsActive ? 1 : 0
                    },
                    cancellationToken: ct));

            return existingId.Value;
        }

        const string insertSql = @"
INSERT INTO QueryPatternTerms
(
    PatternId,
    Term,
    TermGroup,
    MatchMode,
    IsRequired,
    IsActive,
    CreatedUtc
)
VALUES
(
    @PatternId,
    @Term,
    @TermGroup,
    @MatchMode,
    @IsRequired,
    @IsActive,
    @CreatedUtc
);
SELECT last_insert_rowid();";

        return await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                insertSql,
                new
                {
                    term.PatternId,
                    Term = normalizedTerm,
                    TermGroup = normalizedGroup,
                    MatchMode = normalizedMatchMode,
                    IsRequired = term.IsRequired ? 1 : 0,
                    IsActive = term.IsActive ? 1 : 0,
                    CreatedUtc = now
                },
                cancellationToken: ct));
    }

    public async Task<bool> SetIsActiveAsync(long id, bool isActive, CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        await EnsureSchemaAsync(connection, ct);

        const string sql = @"
UPDATE QueryPatternTerms
SET IsActive = @IsActive
WHERE Id = @Id;";

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                new
                {
                    Id = id,
                    IsActive = isActive ? 1 : 0
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

    private static string NormalizeMatchMode(string? matchMode)
    {
        var normalized = string.IsNullOrWhiteSpace(matchMode)
            ? "contains"
            : matchMode.Trim().ToLowerInvariant();

        return normalized switch
        {
            "contains" => "contains",
            "exact" => "exact",
            _ => throw new ArgumentException("MatchMode debe ser 'contains' o 'exact'.", nameof(matchMode))
        };
    }

    private static async Task EnsureSchemaAsync(SqliteConnection connection, CancellationToken ct)
    {
        const string sql = @"
CREATE TABLE IF NOT EXISTS QueryPatternTerms (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    PatternId INTEGER NOT NULL,
    Term TEXT NOT NULL,
    TermGroup TEXT NOT NULL,
    MatchMode TEXT NOT NULL DEFAULT 'contains',
    IsRequired INTEGER NOT NULL DEFAULT 1,
    IsActive INTEGER NOT NULL DEFAULT 1,
    CreatedUtc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (PatternId) REFERENCES QueryPatterns(Id)
);

CREATE INDEX IF NOT EXISTS IX_QueryPatternTerms_PatternId_IsActive
    ON QueryPatternTerms(PatternId, IsActive, IsRequired, Id);";

        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct));
    }
}
