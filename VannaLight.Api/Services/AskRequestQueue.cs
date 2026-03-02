using System.Threading.Channels;
namespace VannaLight.Api.Services;

public record AskWorkItem(Guid JobId, string Question, string UserId, string ConnectionId);

public interface IAskRequestQueue
{
    ValueTask EnqueueAsync(AskWorkItem workItem);
    ValueTask<AskWorkItem> DequeueAsync(CancellationToken ct);
}

public class AskRequestQueue : IAskRequestQueue
{
    private readonly Channel<AskWorkItem> _queue = Channel.CreateUnbounded<AskWorkItem>();

    public async ValueTask EnqueueAsync(AskWorkItem workItem) =>
        await _queue.Writer.WriteAsync(workItem);

    public async ValueTask<AskWorkItem> DequeueAsync(CancellationToken ct) =>
        await _queue.Reader.ReadAsync(ct);
}