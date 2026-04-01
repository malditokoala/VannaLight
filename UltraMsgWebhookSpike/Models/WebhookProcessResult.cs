namespace UltraMsgWebhookSpike.Models;

public sealed class WebhookProcessResult
{
    public DateTimeOffset ReceivedAtUtc { get; init; }
    public string? EventType { get; init; }
    public string? From { get; init; }
    public string? Text { get; init; }
    public string? MessageId { get; init; }
    public string? ChatId { get; init; }
    public bool? FromMe { get; init; }
    public bool HasText { get; init; }
    public bool ReplyAttempted { get; init; }
    public string? ReplyText { get; init; }
    public string? ReplySkippedReason { get; init; }
    public UltraMsgSendResult? UltraMsgResponse { get; init; }
}
