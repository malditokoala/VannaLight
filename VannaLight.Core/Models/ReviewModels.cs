namespace VannaLight.Core.Models;

public enum ReviewStatus
{
    Pending,
    Approved,
    Rejected
}

public enum ReviewReason
{
    Unsafe,
    NotCompiling,
    UserMarkedIncorrect
}

public sealed record ReviewItem
{
    public long Id { get; init; }
    public string Question { get; init; } = string.Empty;
    public string GeneratedSql { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
    public string Status { get; init; } = ReviewStatus.Pending.ToString();
    public string Reason { get; init; } = string.Empty;
    public DateTime CreatedUtc { get; init; }
}