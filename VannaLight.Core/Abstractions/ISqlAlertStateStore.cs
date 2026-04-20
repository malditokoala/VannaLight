using VannaLight.Core.Models;

namespace VannaLight.Core.Abstractions;

public interface ISqlAlertStateStore
{
    Task<SqlAlertState?> GetByRuleIdAsync(long ruleId, CancellationToken ct = default);
    Task UpsertAsync(SqlAlertState state, CancellationToken ct = default);
}
