namespace VannaLight.Core.Models;

public sealed record QueryPatternTerm
{
    public long Id { get; init; }
    public long PatternId { get; init; }
    public string Term { get; init; } = string.Empty;
    public string TermGroup { get; init; } = string.Empty;
    public string MatchMode { get; init; } = "contains";
    public bool IsRequired { get; init; } = true;
    public bool IsActive { get; init; } = true;
    public DateTime CreatedUtc { get; init; }
}
