namespace VannaLight.Core.Models;

public class SemanticHint
{
    public long Id { get; set; }
    public string Domain { get; set; } = string.Empty;
    public string HintKey { get; set; } = string.Empty;
    public string HintType { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? ObjectName { get; set; }
    public string? ColumnName { get; set; }
    public string HintText { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsActive { get; set; }
}
