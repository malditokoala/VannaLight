using VannaLight.Core.Models;

namespace VannaLight.Core.Abstractions;

public interface ITenantDomainStore
{
    Task<TenantDomain?> GetDefaultByTenantAsync(string tenantKey, CancellationToken ct = default);
    Task<TenantDomain?> GetByTenantAndDomainAsync(string tenantKey, string domain, CancellationToken ct = default);
    Task<IReadOnlyList<TenantDomain>> GetAllByTenantAsync(string tenantKey, CancellationToken ct = default);
    Task<int> UpsertAsync(TenantDomain tenantDomain, CancellationToken ct = default);
}
