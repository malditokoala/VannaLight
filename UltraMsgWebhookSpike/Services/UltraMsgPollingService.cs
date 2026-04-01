using Microsoft.Extensions.Options;
using UltraMsgWebhookSpike.Models;
using UltraMsgWebhookSpike.Options;

namespace UltraMsgWebhookSpike.Services;

public sealed class UltraMsgPollingService
{
    private readonly UltraMsgClient _ultraMsgClient;
    private readonly UltraMsgOptions _options;
    private readonly UltraMsgPollingState _state;
    private readonly ILogger<UltraMsgPollingService> _logger;

    public UltraMsgPollingService(
        UltraMsgClient ultraMsgClient,
        IOptions<UltraMsgOptions> options,
        UltraMsgPollingState state,
        ILogger<UltraMsgPollingService> logger)
    {
        _ultraMsgClient = ultraMsgClient;
        _options = options.Value;
        _state = state;
        _logger = logger;
    }

    public async Task<PollingRunResult> RunOnceAsync(CancellationToken cancellationToken)
    {
        var notes = new List<string>();

        if (string.IsNullOrWhiteSpace(_options.InstanceId))
        {
            notes.Add("Falta configurar UltraMsg:InstanceId.");
            return CreateResult(false, false, 0, 0, 0, 0, notes);
        }

        if (string.IsNullOrWhiteSpace(_options.Token))
        {
            notes.Add("Falta configurar UltraMsg:Token.");
            return CreateResult(false, false, 0, 0, 0, 0, notes);
        }

        var warmupMode = !_state.IsWarm && _options.PollingWarmupSkipExisting;
        var chatIds = await ResolveChatIdsAsync(cancellationToken);
        var chatsScanned = 0;
        var messagesScanned = 0;
        var newMessagesDetected = 0;
        var repliesAttempted = 0;

        if (chatIds.Count == 0)
        {
            notes.Add("No se encontraron chats para revisar.");
        }

        foreach (var chatId in chatIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            chatsScanned++;
            var messages = await _ultraMsgClient.GetChatMessagesAsync(chatId, _options.PollingMessageLimit, cancellationToken);
            messagesScanned += messages.Count;

            foreach (var message in messages
                .OrderBy(message => message.Timestamp ?? 0)
                .ThenBy(message => message.Id, StringComparer.Ordinal))
            {
                var messageKey = BuildMessageKey(chatId, message);

                if (!_state.TryMarkProcessed(messageKey))
                {
                    continue;
                }

                if (warmupMode)
                {
                    continue;
                }

                if (message.FromMe == true || string.IsNullOrWhiteSpace(message.Body))
                {
                    continue;
                }

                var to = message.From ?? chatId;
                if (string.IsNullOrWhiteSpace(to))
                {
                    notes.Add($"Mensaje omitido sin remitente en chat {chatId}.");
                    continue;
                }

                newMessagesDetected++;

                _logger.LogInformation(
                    "Polling detecto mensaje nuevo. ChatId={ChatId}, MessageId={MessageId}, From={From}, Body={Body}",
                    chatId,
                    message.Id ?? "(sin id)",
                    message.From ?? "(sin from)",
                    message.Body);

                if (!_options.EnableReply)
                {
                    notes.Add($"Mensaje detectado en {chatId}, pero la respuesta automatica esta deshabilitada.");
                    continue;
                }

                var replyText = $"{_options.FixedReplyPrefix}{message.Body}";
                var sendResult = await _ultraMsgClient.SendTextMessageAsync(
                    to,
                    replyText,
                    _options.IncludeReplyToMessageId ? message.Id : null,
                    cancellationToken);

                repliesAttempted++;

                if (!sendResult.IsSuccess)
                {
                    notes.Add($"Error respondiendo mensaje {message.Id ?? "(sin id)"} en {chatId}: {sendResult.ErrorMessage ?? "error no especificado"}");
                }
            }
        }

        if (!_state.IsWarm)
        {
            _state.MarkWarm();
        }

        if (warmupMode)
        {
            notes.Add("Warmup inicial completado. Se registraron mensajes existentes sin responderlos.");
        }

        return CreateResult(true, warmupMode, chatsScanned, messagesScanned, newMessagesDetected, repliesAttempted, notes);
    }

    private async Task<List<string>> ResolveChatIdsAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_options.PollingTargetChatId))
        {
            return [_options.PollingTargetChatId.Trim()];
        }

        return await _ultraMsgClient.GetChatIdsAsync(cancellationToken);
    }

    private static string BuildMessageKey(string chatId, UltraMsgChatMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.Id))
        {
            return message.Id;
        }

        return $"{chatId}|{message.Timestamp}|{message.From}|{message.Body}";
    }

    private static PollingRunResult CreateResult(
        bool success,
        bool warmupMode,
        int chatsScanned,
        int messagesScanned,
        int newMessagesDetected,
        int repliesAttempted,
        List<string> notes)
    {
        return new PollingRunResult
        {
            ExecutedAtUtc = DateTimeOffset.UtcNow,
            Success = success,
            WarmupMode = warmupMode,
            ChatsScanned = chatsScanned,
            MessagesScanned = messagesScanned,
            NewMessagesDetected = newMessagesDetected,
            RepliesAttempted = repliesAttempted,
            Notes = notes
        };
    }
}
