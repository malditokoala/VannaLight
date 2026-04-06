using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Api.Services.Predictions;

public sealed class CompositeDomainPackProvider : IDomainPackProvider
{
    private readonly IndustrialDomainPackAdapter _industrialAdapter;
    private readonly NorthwindSalesDomainPackAdapter _northwindAdapter;
    private readonly IMlTrainingProfileProvider _mlTrainingProfileProvider;

    public CompositeDomainPackProvider(
        IndustrialDomainPackAdapter industrialAdapter,
        NorthwindSalesDomainPackAdapter northwindAdapter,
        IMlTrainingProfileProvider mlTrainingProfileProvider)
    {
        _industrialAdapter = industrialAdapter;
        _northwindAdapter = northwindAdapter;
        _mlTrainingProfileProvider = mlTrainingProfileProvider;
    }

    public async Task<DomainPackDefinition> GetDomainPackAsync(string domain, CancellationToken ct = default)
    {
        var normalizedDomain = string.IsNullOrWhiteSpace(domain) ? "industrial-kpi" : domain.Trim();

        if (normalizedDomain.Contains("northwind", StringComparison.OrdinalIgnoreCase))
        {
            var profile = await _mlTrainingProfileProvider.GetActiveProfileAsync(ct);
            var connectionName = string.IsNullOrWhiteSpace(profile.ConnectionName) ? "NorthwindDb" : profile.ConnectionName;
            return _northwindAdapter.Build(normalizedDomain, connectionName);
        }

        return await _industrialAdapter.GetDomainPackAsync(normalizedDomain, ct);
    }
}
