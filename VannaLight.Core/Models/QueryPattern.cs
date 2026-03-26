namespace VannaLight.Core.Models;

public sealed record QueryPattern
{
    public long Id { get; init; }
    public string Domain { get; init; } = string.Empty;
    public string PatternKey { get; init; } = string.Empty;
    public string IntentName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string SqlTemplate { get; init; } = string.Empty;
    public int? DefaultTopN { get; init; }
    public string? MetricKey { get; init; }
    public string? DimensionKey { get; init; }
    public string? DefaultTimeScopeKey { get; init; }
    public int Priority { get; init; } = 100;
    public bool IsActive { get; init; } = true;
    public DateTime CreatedUtc { get; init; }
    public DateTime? UpdatedUtc { get; init; }
}
