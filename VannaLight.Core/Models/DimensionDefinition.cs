namespace VannaLight.Core.Models;

public sealed class DimensionDefinition
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SqlExpression { get; set; } = string.Empty;
}
