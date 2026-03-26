using VannaLight.Core.Models;

namespace VannaLight.Core.Abstractions;

public interface IQueryPatternTermStore
{
    Task<IReadOnlyList<QueryPatternTerm>> GetActiveByPatternIdsAsync(
        IReadOnlyCollection<long> patternIds,
        CancellationToken ct = default);

    Task<IReadOnlyList<QueryPatternTerm>> GetAllByPatternIdAsync(long patternId, CancellationToken ct = default);
    Task<long> UpsertAsync(QueryPatternTerm term, CancellationToken ct = default);
    Task<bool> SetIsActiveAsync(long id, bool isActive, CancellationToken ct = default);
}
