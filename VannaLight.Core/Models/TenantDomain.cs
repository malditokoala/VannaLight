namespace VannaLight.Core.Models;

public class TenantDomain
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Domain { get; set; } = string.Empty;
    public string ConnectionName { get; set; } = string.Empty;
    public string? SystemProfileKey { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public string ManagementMode { get; set; } = Tenant.UserManagedMode;
    public string CreatedUtc { get; set; } = string.Empty;
    public string UpdatedUtc { get; set; } = string.Empty;
}
