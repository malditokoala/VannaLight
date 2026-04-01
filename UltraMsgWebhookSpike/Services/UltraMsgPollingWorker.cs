using Microsoft.Extensions.Options;
using UltraMsgWebhookSpike.Options;

namespace UltraMsgWebhookSpike.Services;

public sealed class UltraMsgPollingWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly UltraMsgOptions _options;
    private readonly ILogger<UltraMsgPollingWorker> _logger;

    public UltraMsgPollingWorker(
        IServiceProvider serviceProvider,
        IOptions<UltraMsgOptions> options,
        ILogger<UltraMsgPollingWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnablePollingForTests)
        {
            _logger.LogInformation("Polling de pruebas deshabilitado.");
            return;
        }

        var intervalSeconds = Math.Max(3, _options.PollingIntervalSeconds);
        _logger.LogInformation("Polling de pruebas habilitado cada {IntervalSeconds} segundos.", intervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var pollingService = scope.ServiceProvider.GetRequiredService<UltraMsgPollingService>();
                var result = await pollingService.RunOnceAsync(stoppingToken);

                _logger.LogInformation(
                    "Polling ejecutado. Warmup={WarmupMode}, Chats={ChatsScanned}, Messages={MessagesScanned}, New={NewMessagesDetected}, Replies={RepliesAttempted}, Notes={Notes}",
                    result.WarmupMode,
                    result.ChatsScanned,
                    result.MessagesScanned,
                    result.NewMessagesDetected,
                    result.RepliesAttempted,
                    result.Notes.Count == 0 ? "(sin notas)" : string.Join(" | ", result.Notes));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error en polling de UltraMsg.");
            }

            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }
}
