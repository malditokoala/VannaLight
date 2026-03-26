using System.IO;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;
using VannaLight.Core.Settings;

namespace VannaLight.Infrastructure.Data;

public sealed class SqliteQueryPatternStore : IQueryPatternStore
{
    private readonly string _connectionString;

    public SqliteQueryPatternStore(IOptions<SqliteOptions> sqliteOptions)
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

    public async Task<IReadOnlyList<QueryPattern>> GetActiveAsync(string domain, CancellationToken ct = default)
    {
        var normalizedDomain = NormalizeDomain(domain);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        await EnsureSchemaAsync(connection, ct);

        const string sql = @"
SELECT
    Id,
    Domain,
    PatternKey,
    IntentName,
    Description,
    SqlTemplate,
    DefaultTopN,
    MetricKey,
    DimensionKey,
    DefaultTimeScopeKey,
    Priority,
    IsActive,
    CreatedUtc,
    UpdatedUtc
FROM QueryPatterns
WHERE LOWER(TRIM(Domain)) = @Domain
  AND IsActive = 1
ORDER BY Priority ASC, Id ASC;";

        var rows = await connection.QueryAsync<QueryPattern>(
            new CommandDefinition(sql, new { Domain = normalizedDomain }, cancellationToken: ct));

        return rows.AsList();
    }

    public async Task<IReadOnlyList<QueryPattern>> GetAllAsync(string domain, CancellationToken ct = default)
    {
        var normalizedDomain = NormalizeDomain(domain);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        await EnsureSchemaAsync(connection, ct);

        const string sql = @"
SELECT
    Id,
    Domain,
    PatternKey,
    IntentName,
    Description,
    SqlTemplate,
    DefaultTopN,
    MetricKey,
    DimensionKey,
    DefaultTimeScopeKey,
    Priority,
    IsActive,
    CreatedUtc,
    UpdatedUtc
FROM QueryPatterns
WHERE LOWER(TRIM(Domain)) = @Domain
ORDER BY IsActive DESC, Priority ASC, Id ASC;";

        var rows = await connection.QueryAsync<QueryPattern>(
            new CommandDefinition(sql, new { Domain = normalizedDomain }, cancellationToken: ct));

        return rows.AsList();
    }

    public async Task<QueryPattern?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        await EnsureSchemaAsync(connection, ct);

        const string sql = @"
SELECT
    Id,
    Domain,
    PatternKey,
    IntentName,
    Description,
    SqlTemplate,
    DefaultTopN,
    MetricKey,
    DimensionKey,
    DefaultTimeScopeKey,
    Priority,
    IsActive,
    CreatedUtc,
    UpdatedUtc
FROM QueryPatterns
WHERE Id = @Id;";

        return await connection.QuerySingleOrDefaultAsync<QueryPattern>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }

    public async Task<long> UpsertAsync(QueryPattern pattern, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        var normalizedDomain = NormalizeDomain(pattern.Domain);
        var patternKey = NormalizeRequired(pattern.PatternKey, nameof(pattern.PatternKey));
        var intentName = NormalizeRequired(pattern.IntentName, nameof(pattern.IntentName));
        var sqlTemplate = NormalizeRequired(pattern.SqlTemplate, nameof(pattern.SqlTemplate));
        var description = NormalizeOptional(pattern.Description);
        var metricKey = NormalizeOptional(pattern.MetricKey);
        var dimensionKey = NormalizeOptional(pattern.DimensionKey);
        var defaultTimeScopeKey = NormalizeOptional(pattern.DefaultTimeScopeKey);
        var now = DateTime.UtcNow;

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        await EnsureSchemaAsync(connection, ct);

        var existingId = pattern.Id > 0
            ? pattern.Id
            : await connection.ExecuteScalarAsync<long?>(
                new CommandDefinition(
                    @"
SELECT Id
FROM QueryPatterns
WHERE LOWER(TRIM(Domain)) = @Domain
  AND LOWER(TRIM(PatternKey)) = LOWER(TRIM(@PatternKey))
LIMIT 1;",
                    new
                    {
                        Domain = normalizedDomain,
                        PatternKey = patternKey
                    },
                    cancellationToken: ct));

        if (existingId.HasValue && existingId.Value > 0)
        {
            const string updateSql = @"
UPDATE QueryPatterns
SET
    Domain = @Domain,
    PatternKey = @PatternKey,
    IntentName = @IntentName,
    Description = @Description,
    SqlTemplate = @SqlTemplate,
    DefaultTopN = @DefaultTopN,
    MetricKey = @MetricKey,
    DimensionKey = @DimensionKey,
    DefaultTimeScopeKey = @DefaultTimeScopeKey,
    Priority = @Priority,
    IsActive = @IsActive,
    UpdatedUtc = @UpdatedUtc
WHERE Id = @Id;";

            await connection.ExecuteAsync(
                new CommandDefinition(
                    updateSql,
                    new
                    {
                        Id = existingId.Value,
                        Domain = normalizedDomain,
                        PatternKey = patternKey,
                        IntentName = intentName,
                        Description = description,
                        SqlTemplate = sqlTemplate,
                        DefaultTopN = pattern.DefaultTopN,
                        MetricKey = metricKey,
                        DimensionKey = dimensionKey,
                        DefaultTimeScopeKey = defaultTimeScopeKey,
                        Priority = pattern.Priority,
                        IsActive = pattern.IsActive ? 1 : 0,
                        UpdatedUtc = now
                    },
                    cancellationToken: ct));

            return existingId.Value;
        }

        const string insertSql = @"
INSERT INTO QueryPatterns
(
    Domain,
    PatternKey,
    IntentName,
    Description,
    SqlTemplate,
    DefaultTopN,
    MetricKey,
    DimensionKey,
    DefaultTimeScopeKey,
    Priority,
    IsActive,
    CreatedUtc,
    UpdatedUtc
)
VALUES
(
    @Domain,
    @PatternKey,
    @IntentName,
    @Description,
    @SqlTemplate,
    @DefaultTopN,
    @MetricKey,
    @DimensionKey,
    @DefaultTimeScopeKey,
    @Priority,
    @IsActive,
    @CreatedUtc,
    NULL
);
SELECT last_insert_rowid();";

        return await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                insertSql,
                new
                {
                    Domain = normalizedDomain,
                    PatternKey = patternKey,
                    IntentName = intentName,
                    Description = description,
                    SqlTemplate = sqlTemplate,
                    DefaultTopN = pattern.DefaultTopN,
                    MetricKey = metricKey,
                    DimensionKey = dimensionKey,
                    DefaultTimeScopeKey = defaultTimeScopeKey,
                    Priority = pattern.Priority,
                    IsActive = pattern.IsActive ? 1 : 0,
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
UPDATE QueryPatterns
SET
    IsActive = @IsActive,
    UpdatedUtc = @UpdatedUtc
WHERE Id = @Id;";

        var affected = await connection.ExecuteAsync(
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

    private static string NormalizeDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            throw new ArgumentException("Domain es requerido.", nameof(domain));

        return domain.Trim().ToLowerInvariant();
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
CREATE TABLE IF NOT EXISTS QueryPatterns (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Domain TEXT NOT NULL,
    PatternKey TEXT NOT NULL,
    IntentName TEXT NOT NULL,
    Description TEXT NULL,
    SqlTemplate TEXT NOT NULL,
    DefaultTopN INTEGER NULL,
    MetricKey TEXT NULL,
    DimensionKey TEXT NULL,
    DefaultTimeScopeKey TEXT NULL,
    Priority INTEGER NOT NULL DEFAULT 100,
    IsActive INTEGER NOT NULL DEFAULT 1,
    CreatedUtc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedUtc TEXT NULL
);

CREATE INDEX IF NOT EXISTS IX_QueryPatterns_Domain_IsActive_Priority
    ON QueryPatterns(Domain, IsActive, Priority, Id);";

        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct));
    }
}
