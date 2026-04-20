using VannaLight.Core.Models;

namespace VannaLight.Core.Abstractions;

public interface ISqlAlertMetricCatalog
{
    Task<SqlAlertCatalogSnapshot> GetCatalogAsync(string domain, string connectionName, CancellationToken ct = default);
}
