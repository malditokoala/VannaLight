namespace UltraMsgWebhookSpike.Models;

public sealed class PollingRunResult
{
    public DateTimeOffset ExecutedAtUtc { get; init; }
    public bool Success { get; init; }
    public bool WarmupMode { get; init; }
    public int ChatsScanned { get; init; }
    public int MessagesScanned { get; init; }
    public int NewMessagesDetected { get; init; }
    public int RepliesAttempted { get; init; }
    public List<string> Notes { get; init; } = [];
}
