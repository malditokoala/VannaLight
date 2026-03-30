using VannaLight.Core.Models;

namespace VannaLight.Core.Abstractions;

public interface ITenantStore
{
    Task<Tenant?> GetByKeyAsync(string tenantKey, CancellationToken ct = default);
    Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken ct = default);
    Task<int> UpsertAsync(Tenant tenant, CancellationToken ct = default);
}
