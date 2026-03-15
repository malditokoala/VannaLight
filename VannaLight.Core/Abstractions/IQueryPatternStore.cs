using VannaLight.Core.Models;

namespace VannaLight.Core.Abstractions;

public interface IQueryPatternStore
{
    Task<IReadOnlyList<QueryPattern>> GetActiveAsync(CancellationToken ct = default);
}
