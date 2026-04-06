using VannaLight.Core.Models;

namespace VannaLight.Core.Abstractions;

public interface IPredictionProfileStore
{
    Task<IReadOnlyList<PredictionProfile>> GetAllAsync(string sqlitePath, string domain, CancellationToken ct = default);
    Task<PredictionProfile?> GetAsync(string sqlitePath, string domain, string profileKey, CancellationToken ct = default);
    Task<long> UpsertAsync(string sqlitePath, PredictionProfile profile, CancellationToken ct = default);
    Task<bool> SetIsActiveAsync(string sqlitePath, long id, bool isActive, CancellationToken ct = default);
    Task<bool> SetActiveProfileAsync(string sqlitePath, string domain, long id, CancellationToken ct = default);
}
