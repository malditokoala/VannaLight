namespace UltraMsgWebhookSpike.Models;

public sealed class UltraMsgSendResult
{
    public bool IsSuccess { get; init; }
    public int StatusCode { get; init; }
    public string? ResponseBody { get; init; }
    public string? ErrorMessage { get; init; }
}
