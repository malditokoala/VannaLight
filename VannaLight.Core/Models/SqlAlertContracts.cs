namespace VannaLight.Core.Models;

public sealed class SqlAlertQueryPlan
{
    public string Sql { get; set; } = string.Empty;
    public IReadOnlyDictionary<string, object?> Parameters { get; set; } = new Dictionary<string, object?>();
    public string Summary { get; set; } = string.Empty;
    public string BaseObject { get; set; } = string.Empty;
    public string MetricDisplayName { get; set; } = string.Empty;
    public string? DimensionDisplayName { get; set; }
}

public sealed class SqlAlertEvaluationOutcome
{
    public bool Success { get; set; }
    public bool ConditionMet { get; set; }
    public decimal? ObservedValue { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public SqlAlertQueryPlan? QueryPlan { get; set; }
}

public sealed class SqlAlertCatalogSnapshot
{
    public string Domain { get; set; } = string.Empty;
    public string ConnectionName { get; set; } = string.Empty;
    public IReadOnlyList<MetricDefinition> Metrics { get; set; } = Array.Empty<MetricDefinition>();
    public IReadOnlyList<DimensionDefinition> Dimensions { get; set; } = Array.Empty<DimensionDefinition>();
}
