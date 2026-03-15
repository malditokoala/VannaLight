namespace VannaLight.Core.Models;

public sealed record QueryPattern
{
    public long Id { get; init; }
    public string PatternKey { get; init; } = string.Empty;
    public string IntentName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Tags { get; init; } = string.Empty;
    public int Priority { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedUtc { get; init; }
}