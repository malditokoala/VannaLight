using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Infrastructure.Data;

public sealed class SqlAlertMetricCatalog(IDomainPackProvider domainPackProvider) : ISqlAlertMetricCatalog
{
    public async Task<SqlAlertCatalogSnapshot> GetCatalogAsync(string domain, string connectionName, CancellationToken ct = default)
    {
        var pack = await domainPackProvider.GetDomainPackAsync(domain, ct);
        return new SqlAlertCatalogSnapshot
        {
            Domain = pack.Domain,
            ConnectionName = string.IsNullOrWhiteSpace(connectionName) ? pack.ConnectionName : connectionName.Trim(),
            Metrics = pack.Metrics,
            Dimensions = pack.Dimensions
        };
    }
}
