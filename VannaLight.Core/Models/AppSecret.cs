namespace VannaLight.Core.Models;

public class AppSecret
{
    public int Id { get; set; }
    public string SecretKey { get; set; } = string.Empty;
    public string CipherText { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CreatedUtc { get; set; } = string.Empty;
    public string UpdatedUtc { get; set; } = string.Empty;
}
