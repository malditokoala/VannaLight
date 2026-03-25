
using Microsoft.Extensions.Configuration;
using VannaLight.Core.Abstractions;


namespace VannaLight.Infrastructure.Configuration;

public class SystemConfigProvider : ISystemConfigProvider
{
    private readonly ISystemConfigStore _store;
    private readonly IConfiguration _configuration;
    private readonly string _environmentName;

    public SystemConfigProvider(
        ISystemConfigStore store,
        IConfiguration configuration)
    {
        _store = store;
        _configuration = configuration;
        _environmentName = configuration["Bootstrap:EnvironmentName"] ?? "Development";
    }

    public async Task<string?> GetValueAsync(string section, string key, CancellationToken ct = default)
    {
        var profile = await _store.GetActiveProfileAsync(_environmentName, ct);
        if (profile != null)
        {
            var entry = await _store.GetEntryAsync(profile.Id, section, key, ct);
            if (entry != null && !string.IsNullOrWhiteSpace(entry.Value))
                return entry.Value;
        }

        return _configuration[$"{section}:{key}"];
    }

    public async Task<string> GetRequiredValueAsync(string section, string key, CancellationToken ct = default)
    {
        var value = await GetValueAsync(section, key, ct);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Missing required configuration value: {section}:{key}");
        return value;
    }

    public async Task<int?> GetIntAsync(string section, string key, CancellationToken ct = default)
    {
        var value = await GetValueAsync(section, key, ct);
        if (int.TryParse(value, out var parsed))
            return parsed;
        return null;
    }

    public async Task<bool?> GetBoolAsync(string section, string key, CancellationToken ct = default)
    {
        var value = await GetValueAsync(section, key, ct);
        if (bool.TryParse(value, out var parsed))
            return parsed;
        return null;
    }
}
