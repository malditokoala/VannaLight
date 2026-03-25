namespace VannaLight.Core.Models;

public class SystemConfigEntry
{
    public int Id { get; set; }
    public int ProfileId { get; set; }
    public string Section { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string ValueType { get; set; } = "string";
    public bool IsSecret { get; set; }
    public string? SecretRef { get; set; }
    public bool IsEditableInUi { get; set; } = true;
    public string? ValidationRule { get; set; }
    public string? Description { get; set; }
    public string CreatedUtc { get; set; } = string.Empty;
    public string UpdatedUtc { get; set; } = string.Empty;
}