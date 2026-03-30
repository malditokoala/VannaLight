namespace VannaLight.Core.Models;

public class AskExecutionContext
{
    public string TenantKey { get; set; } = "default";
    public string Domain { get; set; } = string.Empty;
    public string ConnectionName { get; set; } = "OperationalDb";
}
