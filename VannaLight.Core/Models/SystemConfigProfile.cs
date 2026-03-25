namespace VannaLight.Core.Models;

public class SystemConfigProfile
{
    public int Id { get; set; }
    public string EnvironmentName { get; set; } = string.Empty;
    public string ProfileKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public bool IsReadOnly { get; set; }
    public string CreatedUtc { get; set; } = string.Empty;
    public string UpdatedUtc { get; set; } = string.Empty;
}