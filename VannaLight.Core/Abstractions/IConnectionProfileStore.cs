using VannaLight.Core.Models;

namespace VannaLight.Core.Abstractions;

public interface IConnectionProfileStore
{
    Task<ConnectionProfile?> GetActiveAsync(string environmentName, string connectionName, CancellationToken ct = default);
    Task<ConnectionProfile?> GetAsync(string environmentName, string profileKey, string connectionName, CancellationToken ct = default);
    Task<IReadOnlyList<ConnectionProfile>> GetAllAsync(string environmentName, CancellationToken ct = default);
    Task<int> UpsertAsync(ConnectionProfile profile, CancellationToken ct = default);
}
