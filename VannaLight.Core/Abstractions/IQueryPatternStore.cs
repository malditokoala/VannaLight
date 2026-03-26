using VannaLight.Core.Models;

namespace VannaLight.Core.Abstractions;

public interface IQueryPatternStore
{
    Task<IReadOnlyList<QueryPattern>> GetActiveAsync(string domain, CancellationToken ct = default);
    Task<IReadOnlyList<QueryPattern>> GetAllAsync(string domain, CancellationToken ct = default);
    Task<QueryPattern?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<long> UpsertAsync(QueryPattern pattern, CancellationToken ct = default);
    Task<bool> SetIsActiveAsync(long id, bool isActive, CancellationToken ct = default);
}
