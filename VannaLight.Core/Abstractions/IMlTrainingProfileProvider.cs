using VannaLight.Core.Settings;

namespace VannaLight.Core.Abstractions;

public interface IMlTrainingProfileProvider
{
    Task<MlTrainingProfile> GetActiveProfileAsync(CancellationToken ct = default);
    Task<string> ResolveConnectionStringAsync(MlTrainingProfile profile, CancellationToken ct = default);
}
