using Microsoft.Extensions.Configuration;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Infrastructure.Configuration;

public class ExecutionContextResolver : IExecutionContextResolver
{
    private readonly ISystemConfigProvider _systemConfigProvider;
    private readonly ITenantStore _tenantStore;
    private readonly ITenantDomainStore _tenantDomainStore;
    private readonly string _defaultSystemProfileKey;

    public ExecutionContextResolver(
        ISystemConfigProvider systemConfigProvider,
        ITenantStore tenantStore,
        ITenantDomainStore tenantDomainStore,
        IConfiguration configuration)
    {
        _systemConfigProvider = systemConfigProvider;
        _tenantStore = tenantStore;
        _tenantDomainStore = tenantDomainStore;
        _defaultSystemProfileKey = configuration["SystemStartup:DefaultSystemProfile"]?.Trim() ?? "default";
    }

    public async Task<AskExecutionContext> ResolveAsync(
        string? tenantKey = null,
        string? domain = null,
        string? connectionName = null,
        CancellationToken ct = default)
    {
        var resolvedTenantKey = NormalizeOrDefault(
            tenantKey,
            await _systemConfigProvider.GetValueAsync("TenantDefaults", "TenantKey", ct: ct),
            "default");

        var tenant = await _tenantStore.GetByKeyAsync(resolvedTenantKey, ct);
        if (tenant is null || !tenant.IsActive)
            resolvedTenantKey = "default";

        var configuredDefaultDomain = await _systemConfigProvider.GetRequiredValueAsync("Retrieval", "Domain", ct: ct);
        var configuredDefaultConnection = NormalizeOrDefault(
            await _systemConfigProvider.GetValueAsync("TenantDefaults", "ConnectionName", ct: ct),
            fallback: "OperationalDb");

        var resolvedDomain = NormalizeOrDefault(domain, configuredDefaultDomain);
        var resolvedConnectionName = NormalizeOrDefault(connectionName, configuredDefaultConnection);
        var resolvedSystemProfileKey = _defaultSystemProfileKey;

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

            resolvedSystemProfileKey = NormalizeOrDefault(mapping.SystemProfileKey, _defaultSystemProfileKey, _defaultSystemProfileKey);
        }

        return new AskExecutionContext
        {
            TenantKey = resolvedTenantKey,
            Domain = NormalizeOrDefault(resolvedDomain, configuredDefaultDomain),
            ConnectionName = NormalizeOrDefault(resolvedConnectionName, configuredDefaultConnection),
            SystemProfileKey = NormalizeOrDefault(resolvedSystemProfileKey, _defaultSystemProfileKey, _defaultSystemProfileKey)
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
