using Microsoft.Extensions.Configuration;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Settings;

namespace VannaLight.Api.Services.Predictions;

public sealed class MlTrainingProfileProvider : IMlTrainingProfileProvider
{
    private readonly ISystemConfigProvider _systemConfigProvider;
    private readonly IOperationalConnectionResolver _connectionResolver;
    private readonly KpiViewOptions _kpiViews;

    public MlTrainingProfileProvider(
        ISystemConfigProvider systemConfigProvider,
        IOperationalConnectionResolver connectionResolver,
        KpiViewOptions kpiViews)
    {
        _systemConfigProvider = systemConfigProvider;
        _connectionResolver = connectionResolver;
        _kpiViews = kpiViews;
    }

    public async Task<MlTrainingProfile> GetActiveProfileAsync(CancellationToken ct = default)
    {
        var sourceMode = (await _systemConfigProvider.GetValueAsync("ML", "SourceMode", ct: ct))?.Trim();
        var connectionName = (await _systemConfigProvider.GetValueAsync("ML", "ConnectionName", ct: ct))?.Trim();
        var profileName = (await _systemConfigProvider.GetValueAsync("ML", "ProfileName", ct: ct))?.Trim();
        var displayName = (await _systemConfigProvider.GetValueAsync("ML", "DisplayName", ct: ct))?.Trim();
        var description = (await _systemConfigProvider.GetValueAsync("ML", "Description", ct: ct))?.Trim();
        var shiftTableName = (await _systemConfigProvider.GetValueAsync("ML", "ShiftTableName", ct: ct))?.Trim();
        var productionViewName = (await _systemConfigProvider.GetValueAsync("ML", "ProductionViewName", ct: ct))?.Trim();
        var scrapViewName = (await _systemConfigProvider.GetValueAsync("ML", "ScrapViewName", ct: ct))?.Trim();
        var downtimeViewName = (await _systemConfigProvider.GetValueAsync("ML", "DowntimeViewName", ct: ct))?.Trim();
        var trainingSql = await _systemConfigProvider.GetValueAsync("ML", "TrainingSql", ct: ct);

        return new MlTrainingProfile
        {
            ProfileName = string.IsNullOrWhiteSpace(profileName) ? "default-forecast" : profileName,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Forecasting Profile" : displayName,
            Description = description,
            SourceMode = string.IsNullOrWhiteSpace(sourceMode) ? MlTrainingProfile.KpiViewsMode : sourceMode,
            ConnectionName = string.IsNullOrWhiteSpace(connectionName) ? null : connectionName,
            ShiftTableName = string.IsNullOrWhiteSpace(shiftTableName) ? "dbo.Turnos" : shiftTableName,
            ProductionViewName = string.IsNullOrWhiteSpace(productionViewName) ? _kpiViews.ProductionViewQualifiedName : productionViewName,
            ScrapViewName = string.IsNullOrWhiteSpace(scrapViewName) ? _kpiViews.ScrapViewQualifiedName : scrapViewName,
            DowntimeViewName = string.IsNullOrWhiteSpace(downtimeViewName) ? _kpiViews.DowntimeViewQualifiedName : downtimeViewName,
            TrainingSql = trainingSql
        };
    }

    public async Task<string> ResolveConnectionStringAsync(MlTrainingProfile profile, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(profile.ConnectionName))
            return await _connectionResolver.ResolveConnectionStringAsync(profile.ConnectionName.Trim(), ct);

        return await _connectionResolver.ResolveOperationalConnectionStringAsync(ct);
    }
}
