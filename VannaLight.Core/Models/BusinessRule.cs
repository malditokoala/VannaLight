namespace VannaLight.Core.Models;

public class BusinessRule
{
    public long Id { get; set; }
    public string Domain { get; set; } = string.Empty;
    public string RuleKey { get; set; } = string.Empty;
    public string RuleText { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsActive { get; set; }
}