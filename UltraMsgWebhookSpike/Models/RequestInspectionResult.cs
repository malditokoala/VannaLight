namespace UltraMsgWebhookSpike.Models;

public sealed class RequestInspectionResult
{
    public string? ContentType { get; init; }
    public required Dictionary<string, string[]> Headers { get; init; }
    public required Dictionary<string, string[]> Query { get; init; }
    public required Dictionary<string, string[]> Form { get; init; }
    public required string RawBody { get; init; }
    public string? ParsedBodyJson { get; init; }
    public required Dictionary<string, string> FlattenedBodyValues { get; init; }
    public required ReceivedMessageInfo Message { get; init; }
    public required string HeadersJson { get; init; }
    public required string QueryJson { get; init; }
    public required string FormJson { get; init; }
    public required string MessageJson { get; init; }
}
