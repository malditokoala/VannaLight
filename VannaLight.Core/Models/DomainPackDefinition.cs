namespace VannaLight.Core.Models;

public sealed class DomainPackDefinition
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string? ConnectionName { get; set; }
    public string CalendarProfileKey { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IReadOnlyList<MetricDefinition> Metrics { get; set; } = Array.Empty<MetricDefinition>();
    public IReadOnlyList<DimensionDefinition> Dimensions { get; set; } = Array.Empty<DimensionDefinition>();
}
