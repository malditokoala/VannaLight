
using Microsoft.Extensions.Configuration;
using System.Globalization;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;


namespace VannaLight.Infrastructure.Configuration;

public class SystemConfigProvider : ISystemConfigProvider
{
    private readonly ISystemConfigStore _store;
    private readonly IConfiguration _configuration;
    private readonly string _environmentName;
    private readonly string _defaultProfileKey;

    public SystemConfigProvider(
        ISystemConfigStore store,
        IConfiguration configuration)
    {
        _store = store;
        _configuration = configuration;
        _environmentName = configuration["SystemStartup:EnvironmentName"] ?? "Development";
        _defaultProfileKey = configuration["SystemStartup:DefaultSystemProfile"] ?? "default";
    }

    public async Task<string?> GetValueAsync(string section, string key, string? profileKey = null, CancellationToken ct = default)
    {
        var profile = await ResolveProfileAsync(profileKey, ct);
        if (profile != null)
        {
            var entry = await _store.GetEntryAsync(profile.Id, section, key, ct);
            if (entry != null && !string.IsNullOrWhiteSpace(entry.Value))
                return entry.Value;
        }

        return _configuration[$"{section}:{key}"];
    }

    public async Task<string> GetRequiredValueAsync(string section, string key, string? profileKey = null, CancellationToken ct = default)
    {
        var value = await GetValueAsync(section, key, profileKey, ct);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Missing required configuration value: {section}:{key}");
        return value;
    }

    public async Task<int?> GetIntAsync(string section, string key, string? profileKey = null, CancellationToken ct = default)
    {
        var value = await GetValueAsync(section, key, profileKey, ct);
        if (int.TryParse(value, out var parsed))
            return parsed;
        return null;
    }

    public async Task<double?> GetDoubleAsync(string section, string key, string? profileKey = null, CancellationToken ct = default)
    {
        var value = await GetValueAsync(section, key, profileKey, ct);
        if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        return null;
    }

    public async Task<bool?> GetBoolAsync(string section, string key, string? profileKey = null, CancellationToken ct = default)
    {
        var value = await GetValueAsync(section, key, profileKey, ct);
        if (bool.TryParse(value, out var parsed))
            return parsed;
        return null;
    }

    private async Task<SystemConfigProfile?> ResolveProfileAsync(string? profileKey, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(profileKey))
        {
            var explicitProfile = await _store.GetProfileAsync(_environmentName, profileKey.Trim(), ct);
            if (explicitProfile != null)
                return explicitProfile;
        }

        return await _store.GetActiveProfileAsync(_environmentName, ct)
            ?? await _store.GetProfileAsync(_environmentName, _defaultProfileKey, ct);
    }
}
