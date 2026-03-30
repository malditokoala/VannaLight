namespace VannaLight.Core.Models;

public class SchemaObjectCandidate
{
    public string SchemaName { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int ColumnCount { get; set; }
    public int PrimaryKeyCount { get; set; }
    public int ForeignKeyCount { get; set; }
    public bool IsCurrentlyAllowed { get; set; }
    public bool IsSuggested { get; set; }
}
