using VannaLight.Core.Models;

namespace VannaLight.Core.Abstractions;

public interface ISystemConfigStore
{
    Task<SystemConfigProfile?> GetActiveProfileAsync(string environmentName, CancellationToken ct = default);
    Task<SystemConfigProfile?> GetProfileAsync(string environmentName, string profileKey, CancellationToken ct = default);
    Task<IReadOnlyList<SystemConfigEntry>> GetEntriesAsync(int profileId, CancellationToken ct = default);
    Task<SystemConfigEntry?> GetEntryAsync(int profileId, string section, string key, CancellationToken ct = default);
    Task<int> UpsertProfileAsync(SystemConfigProfile profile, CancellationToken ct = default);
    Task<int> UpsertEntryAsync(SystemConfigEntry entry, CancellationToken ct = default);
}