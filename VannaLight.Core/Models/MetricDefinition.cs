namespace VannaLight.Core.Models;

public sealed class MetricDefinition
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TimeColumn { get; set; } = string.Empty;
    public string SqlExpression { get; set; } = string.Empty;
    public string BaseObject { get; set; } = string.Empty;
    public string DefaultAggregation { get; set; } = "sum";
    public IReadOnlyList<string> AllowedDimensions { get; set; } = Array.Empty<string>();
}
