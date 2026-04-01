namespace UltraMsgWebhookSpike.Models;

public sealed class UltraMsgChatMessage
{
    public string? Id { get; init; }
    public string? ChatId { get; init; }
    public string? From { get; init; }
    public string? To { get; init; }
    public string? Author { get; init; }
    public string? Body { get; init; }
    public string? Type { get; init; }
    public long? Timestamp { get; init; }
    public bool? FromMe { get; init; }
}
