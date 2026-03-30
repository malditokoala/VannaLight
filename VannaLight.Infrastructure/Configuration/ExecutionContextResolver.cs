using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Infrastructure.Configuration;

public class ExecutionContextResolver : IExecutionContextResolver
{
    private readonly ISystemConfigProvider _systemConfigProvider;
    private readonly ITenantStore _tenantStore;
    private readonly ITenantDomainStore _tenantDomainStore;

    public ExecutionContextResolver(
        ISystemConfigProvider systemConfigProvider,
        ITenantStore tenantStore,
        ITenantDomainStore tenantDomainStore)
    {
        _systemConfigProvider = systemConfigProvider;
        _tenantStore = tenantStore;
        _tenantDomainStore = tenantDomainStore;
    }

    public async Task<AskExecutionContext> ResolveAsync(
        string? tenantKey = null,
        string? domain = null,
        string? connectionName = null,
        CancellationToken ct = default)
    {
        var resolvedTenantKey = NormalizeOrDefault(
            tenantKey,
            await _systemConfigProvider.GetValueAsync("TenantDefaults", "TenantKey", ct),
            "default");

        var tenant = await _tenantStore.GetByKeyAsync(resolvedTenantKey, ct);
        if (tenant is null || !tenant.IsActive)
            resolvedTenantKey = "default";

        var configuredDefaultDomain = await _systemConfigProvider.GetRequiredValueAsync("Retrieval", "Domain", ct);
        var configuredDefaultConnection = NormalizeOrDefault(
            await _systemConfigProvider.GetValueAsync("TenantDefaults", "ConnectionName", ct),
            fallback: "OperationalDb");

        var resolvedDomain = NormalizeOrDefault(domain, configuredDefaultDomain);
        var resolvedConnectionName = NormalizeOrDefault(connectionName, configuredDefaultConnection);

        TenantDomain? mapping = null;
        if (!string.IsNullOrWhiteSpace(resolvedDomain))
        {
            mapping = await _tenantDomainStore.GetByTenantAndDomainAsync(resolvedTenantKey, resolvedDomain, ct);
        }

        mapping ??= await _tenantDomainStore.GetDefaultByTenantAsync(resolvedTenantKey, ct);

        if (mapping is not null)
        {
            if (string.IsNullOrWhiteSpace(domain))
                resolvedDomain = mapping.Domain;

            if (string.IsNullOrWhiteSpace(connectionName))
                resolvedConnectionName = NormalizeOrDefault(mapping.ConnectionName, configuredDefaultConnection);
        }

        return new AskExecutionContext
        {
            TenantKey = resolvedTenantKey,
            Domain = NormalizeOrDefault(resolvedDomain, configuredDefaultDomain),
            ConnectionName = NormalizeOrDefault(resolvedConnectionName, configuredDefaultConnection)
        };
    }

    private static string NormalizeOrDefault(string? primary, string? fallback, string hardDefault = "")
    {
        if (!string.IsNullOrWhiteSpace(primary))
            return primary.Trim();

        if (!string.IsNullOrWhiteSpace(fallback))
            return fallback.Trim();

        return hardDefault;
    }
}
