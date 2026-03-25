namespace VannaLight.Core.Abstractions;

public interface ISystemConfigProvider
{
    Task<string?> GetValueAsync(string section, string key, CancellationToken ct = default);
    Task<string> GetRequiredValueAsync(string section, string key, CancellationToken ct = default);
    Task<int?> GetIntAsync(string section, string key, CancellationToken ct = default);
    Task<bool?> GetBoolAsync(string section, string key, CancellationToken ct = default);
}