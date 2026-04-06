using System.Text.RegularExpressions;
using VannaLight.Core.Models;

namespace VannaLight.Core.Settings;

public sealed class KpiViewOptions
{
    private static readonly Regex QualifiedNameRegex = new(@"^[\[\]\w\.]+$", RegexOptions.Compiled);
    private const string DefaultProductionView = "dbo.vw_KpiProduction_v1";
    private const string DefaultScrapView = "dbo.vw_KpiScrap_v1";
    private const string DefaultDowntimeView = "dbo.vw_KpiDownTime_v1";

    public string? ProductionViewName { get; init; }
    public string? ScrapViewName { get; init; }
    public string? DowntimeViewName { get; init; }

    public string ProductionViewQualifiedName => NormalizeOrDefault(ProductionViewName, DefaultProductionView);
    public string ScrapViewQualifiedName => NormalizeOrDefault(ScrapViewName, DefaultScrapView);
    public string DowntimeViewQualifiedName => NormalizeOrDefault(DowntimeViewName, DefaultDowntimeView);

    public string ResolveByAlias(string? alias)
    {
        return alias switch
        {
            "p" => ProductionViewQualifiedName,
            "s" => ScrapViewQualifiedName,
            "d" => DowntimeViewQualifiedName,
            _ => ProductionViewQualifiedName
        };
    }

    public string? ResolveByMetric(PatternMetric metric)
    {
        return metric switch
        {
            PatternMetric.ScrapQty or PatternMetric.ScrapCost => ScrapViewQualifiedName,
            PatternMetric.ProducedQty => ProductionViewQualifiedName,
            PatternMetric.DownTimeMinutes or PatternMetric.DownTimeCost or PatternMetric.TotalLoss => DowntimeViewQualifiedName,
            _ => null
        };
    }

    public static string NormalizeQualifiedObjectName(string? value, string fallback)
    {
        return NormalizeOrDefault(value, fallback);
    }

    private static string NormalizeOrDefault(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var trimmed = value.Trim();
        return QualifiedNameRegex.IsMatch(trimmed) ? trimmed : fallback;
    }
}
