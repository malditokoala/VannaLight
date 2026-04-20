namespace VannaLight.Core.Models;

public enum SqlAlertComparisonOperator
{
    GreaterThan = 1,
    GreaterThanOrEqual = 2,
    LessThan = 3,
    LessThanOrEqual = 4,
    Equal = 5,
    NotEqual = 6
}

public enum SqlAlertTimeScope
{
    Today = 1,
    Last24Hours = 2,
    CurrentShift = 3,
    CurrentWeek = 4,
    CurrentMonth = 5
}

public enum SqlAlertLifecycleState
{
    Closed = 1,
    Open = 2,
    Acknowledged = 3
}

public enum SqlAlertEventType
{
    Triggered = 1,
    Acknowledged = 2,
    Resolved = 3,
    Cleared = 4,
    EvaluationFailed = 5
}

public static class SqlAlertMetricKeys
{
    public const string ScrapQty = "scrap_qty";
    public const string ProducedQty = "produced_qty";
    public const string DowntimeMinutes = "downtime_minutes";
}
