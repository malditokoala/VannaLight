namespace VannaLight.Core.Models;

public enum PatternTimeScope
{
    Unknown = 0,
    Today = 1,
    Yesterday = 2,
    CurrentWeek = 3,
    CurrentMonth = 4,
    CurrentShift = 5
}

public enum PatternMetric
{
    Unknown = 0,
    ScrapQty = 1,
    ScrapCost = 2,
    ProducedQty = 3,
    DownTimeMinutes = 4,
    DownTimeCost = 5,
    TotalLoss = 6
}

public enum PatternDimension
{
    Unknown = 0,
    Press = 1,
    Mold = 2,
    Failure = 3,
    Department = 4,
    PartNumber = 5
}

public sealed record PatternMatchResult
{
    public bool IsMatch { get; init; }
    public string PatternKey { get; init; } = string.Empty;
    public string IntentName { get; init; } = string.Empty;
    public string SqlTemplate { get; init; } = string.Empty;
    public int TopN { get; init; } = 0;
    public PatternMetric Metric { get; init; } = PatternMetric.Unknown;
    public PatternDimension Dimension { get; init; } = PatternDimension.Unknown;
    public string DimensionValue { get; init; } = string.Empty;
    public PatternTimeScope TimeScope { get; init; } = PatternTimeScope.Unknown;
}
