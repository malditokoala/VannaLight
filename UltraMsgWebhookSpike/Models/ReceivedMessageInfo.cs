namespace UltraMsgWebhookSpike.Models;

public sealed class ReceivedMessageInfo
{
    public string? EventType { get; init; }
    public string? From { get; init; }
    public string? Text { get; init; }
    public string? MessageId { get; init; }
    public string? ChatId { get; init; }
    public bool? FromMe { get; init; }
    public bool HasText => !string.IsNullOrWhiteSpace(Text);
}
