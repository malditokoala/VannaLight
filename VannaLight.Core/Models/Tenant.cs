namespace VannaLight.Core.Models;

public class Tenant
{
    public const string SeedManagedMode = "SeedManaged";
    public const string UserManagedMode = "UserManaged";

    public int Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public string ManagementMode { get; set; } = UserManagedMode;
    public string CreatedUtc { get; set; } = string.Empty;
    public string UpdatedUtc { get; set; } = string.Empty;
}
