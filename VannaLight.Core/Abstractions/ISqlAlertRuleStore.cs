using VannaLight.Core.Models;

namespace VannaLight.Core.Abstractions;

public interface ISqlAlertRuleStore
{
    Task<IReadOnlyList<SqlAlertRule>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SqlAlertRule>> GetActiveAsync(CancellationToken ct = default);
    Task<SqlAlertRule?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<long> UpsertAsync(SqlAlertRule rule, CancellationToken ct = default);
    Task<bool> SetIsActiveAsync(long id, bool isActive, CancellationToken ct = default);
}
