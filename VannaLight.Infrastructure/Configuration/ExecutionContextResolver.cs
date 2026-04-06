using Microsoft.Extensions.Configuration;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Infrastructure.Configuration;

public class ExecutionContextResolver : IExecutionContextResolver
{
    private readonly ISystemConfigProvider _systemConfigProvider;
    private readonly ITenantStore _tenantStore;
    private readonly ITenantDomainStore _tenantDomainStore;
    private readonly IConnectionProfileStore _connectionProfileStore;
    private readonly IConfiguration _configuration;
    private readonly string _environmentName;
    private readonly string _defaultSystemProfileKey;

    public ExecutionContextResolver(
        ISystemConfigProvider systemConfigProvider,
        ITenantStore tenantStore,
        ITenantDomainStore tenantDomainStore,
        IConnectionProfileStore connectionProfileStore,
        IConfiguration configuration)
    {
        _systemConfigProvider = systemConfigProvider;
        _tenantStore = tenantStore;
        _tenantDomainStore = tenantDomainStore;
        _connectionProfileStore = connectionProfileStore;
        _configuration = configuration;
        _environmentName = configuration["SystemStartup:EnvironmentName"]?.Trim() ?? "Development";
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
            fallback: null);
        if (!await IsConfiguredConnectionAsync(configuredDefaultConnection, ct))
            configuredDefaultConnection = string.Empty;

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
            {
                var mappedConnectionName = NormalizeOrDefault(mapping.ConnectionName, configuredDefaultConnection);
                resolvedConnectionName = await IsConfiguredConnectionAsync(mappedConnectionName, ct)
                    ? mappedConnectionName
                    : configuredDefaultConnection;
            }

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

    private async Task<bool> IsConfiguredConnectionAsync(string? connectionName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(connectionName))
            return false;

        var normalizedConnectionName = connectionName.Trim();
        var activeProfile = await _connectionProfileStore.GetActiveAsync(_environmentName, normalizedConnectionName, ct);
        if (activeProfile is not null)
            return true;

        var profile = await _connectionProfileStore.GetAsync(_environmentName, _defaultSystemProfileKey, normalizedConnectionName, ct);
        if (profile is not null)
            return true;

        return !string.IsNullOrWhiteSpace(_configuration.GetConnectionString(normalizedConnectionName));
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
