namespace VannaLight.Core.Models;

public sealed class SqlAlertState
{
    public long RuleId { get; set; }
    public string RuleKey { get; set; } = string.Empty;
    public SqlAlertLifecycleState LifecycleState { get; set; } = SqlAlertLifecycleState.Closed;
    public decimal? LastObservedValue { get; set; }
    public string? LastEvaluationUtc { get; set; }
    public string? LastTriggeredUtc { get; set; }
    public string? LastAcknowledgedUtc { get; set; }
    public string? LastResolvedUtc { get; set; }
    public string? LastClearedUtc { get; set; }
    public string? LastErrorUtc { get; set; }
    public string? LastErrorMessage { get; set; }
    public string? OpenEventKey { get; set; }
    public string? UpdatedUtc { get; set; }
}
