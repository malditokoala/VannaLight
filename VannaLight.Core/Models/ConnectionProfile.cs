namespace VannaLight.Core.Models;

public class ConnectionProfile
{
    public int Id { get; set; }
    public string EnvironmentName { get; set; } = string.Empty;
    public string ProfileKey { get; set; } = string.Empty;
    public string ConnectionName { get; set; } = string.Empty;
    public string ProviderKind { get; set; } = "SqlServer";
    public string ConnectionMode { get; set; } = "CompositeSqlServer";
    public string? ServerHost { get; set; }
    public string? DatabaseName { get; set; }
    public string? UserName { get; set; }
    public bool IntegratedSecurity { get; set; }
    public bool Encrypt { get; set; } = true;
    public bool TrustServerCertificate { get; set; }
    public int CommandTimeoutSec { get; set; } = 30;
    public string? SecretRef { get; set; }
    public bool IsActive { get; set; }
    public string? Description { get; set; }
    public string CreatedUtc { get; set; } = string.Empty;
    public string UpdatedUtc { get; set; } = string.Empty;
}