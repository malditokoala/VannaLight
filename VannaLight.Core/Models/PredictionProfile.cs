namespace VannaLight.Core.Models;

public sealed class PredictionProfile
{
    public long Id { get; set; }
    public string Domain { get; set; } = string.Empty;
    public string ProfileKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string DomainPackKey { get; set; } = string.Empty;
    public string TargetMetricKey { get; set; } = string.Empty;
    public string CalendarProfileKey { get; set; } = string.Empty;
    public string Grain { get; set; } = "day";
    public int Horizon { get; set; } = 7;
    public string HorizonUnit { get; set; } = "day";
    public string ModelType { get; set; } = "FastTree";
    public string? ConnectionName { get; set; }
    public string? SourceMode { get; set; }
    public string? TargetSeriesSource { get; set; }
    public string? FeatureSourcesJson { get; set; }
    public string? GroupByJson { get; set; }
    public string? FiltersJson { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreatedUtc { get; set; } = string.Empty;
    public string UpdatedUtc { get; set; } = string.Empty;
}
