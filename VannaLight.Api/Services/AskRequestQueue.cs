using System.Threading.Channels;
using VannaLight.Api.Contracts;

namespace VannaLight.Api.Services;

public record AskWorkItem(
    Guid JobId,
    string Question,
    string UserId,
    string ConnectionId,
    AskMode Mode,
    string TenantKey,
    string Domain,
    string ConnectionName,
    string SystemProfileKey);

public interface IAskRequestQueue
{
    ValueTask EnqueueAsync(AskWorkItem workItem);
    ValueTask<AskWorkItem> DequeueAsync(CancellationToken ct);
}

public class AskRequestQueue : IAskRequestQueue
{
    private readonly Channel<AskWorkItem> _queue = Channel.CreateUnbounded<AskWorkItem>();

    public ValueTask EnqueueAsync(AskWorkItem workItem) => _queue.Writer.WriteAsync(workItem);

    public ValueTask<AskWorkItem> DequeueAsync(CancellationToken ct) => _queue.Reader.ReadAsync(ct);
}
