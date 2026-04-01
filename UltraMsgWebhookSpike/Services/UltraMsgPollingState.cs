namespace UltraMsgWebhookSpike.Services;

public sealed class UltraMsgPollingState
{
    private readonly HashSet<string> _processedMessageKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();

    public bool IsWarm { get; private set; }

    public bool TryMarkProcessed(string key)
    {
        lock (_sync)
        {
            return _processedMessageKeys.Add(key);
        }
    }

    public void MarkWarm()
    {
        lock (_sync)
        {
            IsWarm = true;
        }
    }
}
