using VannaLight.Core.Models;

namespace VannaLight.Core.Abstractions;

public interface IAppSecretStore
{
    Task<AppSecret?> GetByKeyAsync(string secretKey, CancellationToken ct = default);
    Task<int> UpsertAsync(AppSecret secret, CancellationToken ct = default);
}
