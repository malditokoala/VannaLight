using Dapper;
using Microsoft.Data.Sqlite;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;
using VannaLight.Core.Settings;

namespace VannaLight.Infrastructure.Data;

public sealed class SqliteSqlAlertStateStore : ISqlAlertStateStore
{
    private readonly string _connectionString;

    public SqliteSqlAlertStateStore(RuntimeDbOptions options)
    {
        _connectionString = $"Data Source={options.DbPath}";
    }

    public async Task<SqlAlertState?> GetByRuleIdAsync(long ruleId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT
    RuleId,
    RuleKey,
    LifecycleState,
    LastObservedValue,
    LastEvaluationUtc,
    LastTriggeredUtc,
    LastAcknowledgedUtc,
    LastResolvedUtc,
    LastClearedUtc,
    LastErrorUtc,
    LastErrorMessage,
    OpenEventKey,
    UpdatedUtc
FROM SqlAlertStates
WHERE RuleId = @RuleId
LIMIT 1;";

        await using var conn = new SqliteConnection(_connectionString);
        return await conn.QueryFirstOrDefaultAsync<SqlAlertState>(new CommandDefinition(sql, new { RuleId = ruleId }, cancellationToken: ct));
    }

    public async Task UpsertAsync(SqlAlertState state, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        const string sql = @"
INSERT INTO SqlAlertStates
(
    RuleId,
    RuleKey,
    LifecycleState,
    LastObservedValue,
    LastEvaluationUtc,
    LastTriggeredUtc,
    LastAcknowledgedUtc,
    LastResolvedUtc,
    LastClearedUtc,
    LastErrorUtc,
    LastErrorMessage,
    OpenEventKey,
    UpdatedUtc
)
VALUES
(
    @RuleId,
    @RuleKey,
    @LifecycleState,
    @LastObservedValue,
    @LastEvaluationUtc,
    @LastTriggeredUtc,
    @LastAcknowledgedUtc,
    @LastResolvedUtc,
    @LastClearedUtc,
    @LastErrorUtc,
    @LastErrorMessage,
    @OpenEventKey,
    @UpdatedUtc
)
ON CONFLICT(RuleId)
DO UPDATE SET
    RuleKey = excluded.RuleKey,
    LifecycleState = excluded.LifecycleState,
    LastObservedValue = excluded.LastObservedValue,
    LastEvaluationUtc = excluded.LastEvaluationUtc,
    LastTriggeredUtc = excluded.LastTriggeredUtc,
    LastAcknowledgedUtc = excluded.LastAcknowledgedUtc,
    LastResolvedUtc = excluded.LastResolvedUtc,
    LastClearedUtc = excluded.LastClearedUtc,
    LastErrorUtc = excluded.LastErrorUtc,
    LastErrorMessage = excluded.LastErrorMessage,
    OpenEventKey = excluded.OpenEventKey,
    UpdatedUtc = excluded.UpdatedUtc;";

        state.UpdatedUtc ??= DateTime.UtcNow.ToString("O");

        await using var conn = new SqliteConnection(_connectionString);
        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            state.RuleId,
            state.RuleKey,
            LifecycleState = (int)state.LifecycleState,
            state.LastObservedValue,
            state.LastEvaluationUtc,
            state.LastTriggeredUtc,
            state.LastAcknowledgedUtc,
            state.LastResolvedUtc,
            state.LastClearedUtc,
            state.LastErrorUtc,
            state.LastErrorMessage,
            state.OpenEventKey,
            state.UpdatedUtc
        }, cancellationToken: ct));
    }
}
