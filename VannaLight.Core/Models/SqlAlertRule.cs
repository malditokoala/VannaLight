namespace VannaLight.Core.Models;

public sealed class SqlAlertRule
{
    public long Id { get; set; }
    public string RuleKey { get; set; } = string.Empty;
    public string TenantKey { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string ConnectionName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string MetricKey { get; set; } = string.Empty;
    public string? DimensionKey { get; set; }
    public string? DimensionValue { get; set; }
    public SqlAlertComparisonOperator ComparisonOperator { get; set; } = SqlAlertComparisonOperator.GreaterThan;
    public decimal Threshold { get; set; }
    public SqlAlertTimeScope TimeScope { get; set; } = SqlAlertTimeScope.Today;
    public int EvaluationFrequencyMinutes { get; set; } = 5;
    public int CooldownMinutes { get; set; } = 30;
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public string CreatedUtc { get; set; } = string.Empty;
    public string UpdatedUtc { get; set; } = string.Empty;

    public void ThrowIfInvalid()
    {
        if (string.IsNullOrWhiteSpace(TenantKey))
            throw new ArgumentException("TenantKey es obligatorio.");
        if (string.IsNullOrWhiteSpace(Domain))
            throw new ArgumentException("Domain es obligatorio.");
        if (string.IsNullOrWhiteSpace(ConnectionName))
            throw new ArgumentException("ConnectionName es obligatorio.");
        if (string.IsNullOrWhiteSpace(DisplayName))
            throw new ArgumentException("DisplayName es obligatorio.");
        if (string.IsNullOrWhiteSpace(MetricKey))
            throw new ArgumentException("MetricKey es obligatorio.");
        if (!string.IsNullOrWhiteSpace(DimensionKey) && string.IsNullOrWhiteSpace(DimensionValue))
            throw new ArgumentException("DimensionValue es obligatorio cuando se especifica DimensionKey.");
        if (EvaluationFrequencyMinutes <= 0)
            throw new ArgumentException("EvaluationFrequencyMinutes debe ser mayor a 0.");
        if (CooldownMinutes < 0)
            throw new ArgumentException("CooldownMinutes no puede ser negativo.");
        if (Threshold < 0)
            throw new ArgumentException("Threshold no puede ser negativo.");
    }
}
