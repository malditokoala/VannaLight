using System.Security.Cryptography;
using System.Text;

namespace VannaLight.Core.Settings;

public sealed class MlTrainingProfile
{
    public const string KpiViewsMode = "KpiViews";
    public const string CustomSqlMode = "CustomSql";
    private const string DefaultShiftTable = "dbo.Turnos";

    public string ProfileName { get; init; } = "default-forecast";
    public string DisplayName { get; init; } = "Forecasting Profile";
    public string SourceMode { get; init; } = KpiViewsMode;
    public string? ConnectionName { get; init; }
    public string? Description { get; init; }
    public string? ProductionViewName { get; init; }
    public string? ScrapViewName { get; init; }
    public string? DowntimeViewName { get; init; }
    public string? ShiftTableName { get; init; }
    public string? TrainingSql { get; init; }

    public string NormalizedSourceMode =>
        string.Equals(SourceMode, CustomSqlMode, StringComparison.OrdinalIgnoreCase)
            ? CustomSqlMode
            : KpiViewsMode;

    public bool UsesCustomSql => string.Equals(NormalizedSourceMode, CustomSqlMode, StringComparison.OrdinalIgnoreCase);

    public string ProductionViewQualifiedName =>
        KpiViewOptions.NormalizeQualifiedObjectName(ProductionViewName, "dbo.vw_KpiProduction_v1");

    public string ScrapViewQualifiedName =>
        KpiViewOptions.NormalizeQualifiedObjectName(ScrapViewName, "dbo.vw_KpiScrap_v1");

    public string DowntimeViewQualifiedName =>
        KpiViewOptions.NormalizeQualifiedObjectName(DowntimeViewName, "dbo.vw_KpiDownTime_v1");

    public string ShiftTableQualifiedName =>
        KpiViewOptions.NormalizeQualifiedObjectName(ShiftTableName, DefaultShiftTable);

    public string? TrainingSqlNormalized =>
        string.IsNullOrWhiteSpace(TrainingSql) ? null : TrainingSql.Trim().TrimEnd(';');

    public string GetSignature()
    {
        var canonical = string.Join("||",
            ProfileName.Trim(),
            DisplayName.Trim(),
            NormalizedSourceMode,
            ConnectionName?.Trim() ?? string.Empty,
            Description?.Trim() ?? string.Empty,
            ProductionViewQualifiedName,
            ScrapViewQualifiedName,
            DowntimeViewQualifiedName,
            ShiftTableQualifiedName,
            TrainingSqlNormalized ?? string.Empty);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes);
    }
}
