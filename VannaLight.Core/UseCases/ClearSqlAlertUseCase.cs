using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Core.UseCases;

public sealed class ClearSqlAlertUseCase(
    ISqlAlertRuleStore ruleStore,
    ISqlAlertStateStore stateStore,
    ISqlAlertEventStore eventStore)
{
    public async Task<bool> ExecuteAsync(long ruleId, CancellationToken ct = default)
    {
        var rule = await ruleStore.GetByIdAsync(ruleId, ct);
        if (rule is null)
            return false;

        var state = await stateStore.GetByRuleIdAsync(ruleId, ct) ?? new SqlAlertState
        {
            RuleId = rule.Id,
            RuleKey = rule.RuleKey,
            LifecycleState = SqlAlertLifecycleState.Closed
        };

        var now = DateTime.UtcNow.ToString("O");
        state.LifecycleState = SqlAlertLifecycleState.Closed;
        state.LastClearedUtc = now;
        state.OpenEventKey = null;
        state.UpdatedUtc = now;
        await stateStore.UpsertAsync(state, ct);

        await eventStore.AddAsync(new SqlAlertEvent
        {
            RuleId = rule.Id,
            RuleKey = rule.RuleKey,
            TenantKey = rule.TenantKey,
            Domain = rule.Domain,
            ConnectionName = rule.ConnectionName,
            EventType = SqlAlertEventType.Cleared,
            LifecycleState = SqlAlertLifecycleState.Closed,
            ObservedValue = state.LastObservedValue,
            Threshold = rule.Threshold,
            ComparisonOperator = rule.ComparisonOperator,
            Message = $"Alerta limpiada manualmente: {rule.DisplayName}.",
            EventUtc = now
        }, ct);

        return true;
    }
}
