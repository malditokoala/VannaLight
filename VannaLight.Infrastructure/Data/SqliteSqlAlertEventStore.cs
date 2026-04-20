using Dapper;
using Microsoft.Data.Sqlite;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;
using VannaLight.Core.Settings;

namespace VannaLight.Infrastructure.Data;

public sealed class SqliteSqlAlertEventStore : ISqlAlertEventStore
{
    private readonly string _connectionString;

    public SqliteSqlAlertEventStore(RuntimeDbOptions options)
    {
        _connectionString = $"Data Source={options.DbPath}";
    }

    public async Task<IReadOnlyList<SqlAlertEvent>> GetRecentAsync(string? domain = null, long? ruleId = null, int limit = 100, CancellationToken ct = default)
    {
        const string sql = @"
SELECT
    Id,
    RuleId,
    RuleKey,
    TenantKey,
    Domain,
    ConnectionName,
    EventType,
    LifecycleState,
    ObservedValue,
    Threshold,
    ComparisonOperator,
    Message,
    QuerySummary,
    SqlPreview,
    ErrorText,
    EventUtc
FROM SqlAlertEvents
WHERE (@Domain IS NULL OR @Domain = '' OR Domain = @Domain)
  AND (@RuleId IS NULL OR RuleId = @RuleId)
ORDER BY EventUtc DESC
LIMIT @Limit;";

        await using var conn = new SqliteConnection(_connectionString);
        var rows = await conn.QueryAsync<SqlAlertEvent>(new CommandDefinition(sql, new
        {
            Domain = string.IsNullOrWhiteSpace(domain) ? null : domain.Trim(),
            RuleId = ruleId,
            Limit = Math.Clamp(limit, 1, 500)
        }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<long> AddAsync(SqlAlertEvent alertEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(alertEvent);

        const string sql = @"
INSERT INTO SqlAlertEvents
(
    RuleId,
    RuleKey,
    TenantKey,
    Domain,
    ConnectionName,
    EventType,
    LifecycleState,
    ObservedValue,
    Threshold,
    ComparisonOperator,
    Message,
    QuerySummary,
    SqlPreview,
    ErrorText,
    EventUtc
)
VALUES
(
    @RuleId,
    @RuleKey,
    @TenantKey,
    @Domain,
    @ConnectionName,
    @EventType,
    @LifecycleState,
    @ObservedValue,
    @Threshold,
    @ComparisonOperator,
    @Message,
    @QuerySummary,
    @SqlPreview,
    @ErrorText,
    @EventUtc
);
SELECT last_insert_rowid();";

        alertEvent.EventUtc = string.IsNullOrWhiteSpace(alertEvent.EventUtc)
            ? DateTime.UtcNow.ToString("O")
            : alertEvent.EventUtc;

        await using var conn = new SqliteConnection(_connectionString);
        return await conn.ExecuteScalarAsync<long>(new CommandDefinition(sql, new
        {
            alertEvent.RuleId,
            alertEvent.RuleKey,
            alertEvent.TenantKey,
            alertEvent.Domain,
            alertEvent.ConnectionName,
            EventType = (int)alertEvent.EventType,
            LifecycleState = (int)alertEvent.LifecycleState,
            alertEvent.ObservedValue,
            alertEvent.Threshold,
            ComparisonOperator = (int)alertEvent.ComparisonOperator,
            alertEvent.Message,
            alertEvent.QuerySummary,
            alertEvent.SqlPreview,
            alertEvent.ErrorText,
            alertEvent.EventUtc
        }, cancellationToken: ct));
    }
}
