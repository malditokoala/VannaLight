namespace VannaLight.Core.Models;

public sealed class SqlAlertEvent
{
    public long Id { get; set; }
    public long RuleId { get; set; }
    public string RuleKey { get; set; } = string.Empty;
    public string TenantKey { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string ConnectionName { get; set; } = string.Empty;
    public SqlAlertEventType EventType { get; set; }
    public SqlAlertLifecycleState LifecycleState { get; set; }
    public decimal? ObservedValue { get; set; }
    public decimal Threshold { get; set; }
    public SqlAlertComparisonOperator ComparisonOperator { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? QuerySummary { get; set; }
    public string? SqlPreview { get; set; }
    public string? ErrorText { get; set; }
    public string EventUtc { get; set; } = string.Empty;
}
