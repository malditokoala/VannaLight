namespace UltraMsgWebhookSpike.Options;

public sealed class UltraMsgOptions
{
    public string BaseUrl { get; set; } = "https://api.ultramsg.com";
    public string InstanceId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string? WebhookUrl { get; set; }
    public bool EnableReply { get; set; } = true;
    public bool IncludeReplyToMessageId { get; set; }
    public string FixedReplyPrefix { get; set; } = "Recibi tu mensaje: ";
    public bool EnablePollingForTests { get; set; }
    public int PollingIntervalSeconds { get; set; } = 10;
    public int PollingMessageLimit { get; set; } = 10;
    public bool PollingWarmupSkipExisting { get; set; } = true;
    public string? PollingTargetChatId { get; set; }
}
