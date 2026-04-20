using VannaLight.Core.Models;

namespace VannaLight.Core.Abstractions;

public interface ISqlAlertEventStore
{
    Task<IReadOnlyList<SqlAlertEvent>> GetRecentAsync(string? domain = null, long? ruleId = null, int limit = 100, CancellationToken ct = default);
    Task<long> AddAsync(SqlAlertEvent alertEvent, CancellationToken ct = default);
}
