namespace VannaLight.Core.Models;

public sealed class CalendarProfile
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string CalendarType { get; set; } = string.Empty;
    public string? SourceObject { get; set; }
    public string? Description { get; set; }
}
