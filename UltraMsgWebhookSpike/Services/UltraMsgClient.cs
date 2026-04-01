using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using UltraMsgWebhookSpike.Models;
using UltraMsgWebhookSpike.Options;

namespace UltraMsgWebhookSpike.Services;

public sealed class UltraMsgClient
{
    private readonly HttpClient _httpClient;
    private readonly UltraMsgOptions _options;
    private readonly ILogger<UltraMsgClient> _logger;

    public UltraMsgClient(
        HttpClient httpClient,
        IOptions<UltraMsgOptions> options,
        ILogger<UltraMsgClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<List<string>> GetChatIdsAsync(CancellationToken cancellationToken)
    {
        var endpoint = $"{_options.BaseUrl.TrimEnd('/')}/{_options.InstanceId}/chats/ids?token={Uri.EscapeDataString(_options.Token)}&clear=false";
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return await SendAndParseAsync(
            request,
            endpoint,
            responseBody =>
            {
                using var document = JsonDocument.Parse(responseBody);
                var ids = new List<string>();
                CollectChatIds(document.RootElement, ids);
                return ids
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Select(id => id.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            },
            cancellationToken);
    }

    public async Task<List<UltraMsgChatMessage>> GetChatMessagesAsync(string chatId, int limit, CancellationToken cancellationToken)
    {
        var endpoint =
            $"{_options.BaseUrl.TrimEnd('/')}/{_options.InstanceId}/chats/messages?token={Uri.EscapeDataString(_options.Token)}&chatId={Uri.EscapeDataString(chatId)}&limit={limit}";

        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return await SendAndParseAsync(
            request,
            endpoint,
            responseBody =>
            {
                using var document = JsonDocument.Parse(responseBody);
                var messages = new List<UltraMsgChatMessage>();
                CollectMessages(document.RootElement, chatId, messages);
                return messages;
            },
            cancellationToken);
    }

    public async Task<UltraMsgSendResult> SendTextMessageAsync(
        string to,
        string body,
        string? replyToMessageId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.InstanceId))
        {
            return new UltraMsgSendResult
            {
                IsSuccess = false,
                ErrorMessage = "Falta configurar UltraMsg:InstanceId."
            };
        }

        if (string.IsNullOrWhiteSpace(_options.Token))
        {
            return new UltraMsgSendResult
            {
                IsSuccess = false,
                ErrorMessage = "Falta configurar UltraMsg:Token."
            };
        }

        var endpoint = $"{_options.BaseUrl.TrimEnd('/')}/{_options.InstanceId}/messages/chat";

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var payload = new Dictionary<string, string>
        {
            ["token"] = _options.Token,
            ["to"] = to,
            ["body"] = body
        };

        if (!string.IsNullOrWhiteSpace(replyToMessageId))
        {
            payload["msgId"] = replyToMessageId;
        }

        request.Content = new FormUrlEncodedContent(payload);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogInformation(
                "UltraMsg POST {Endpoint} -> {StatusCode}\n{ResponseBody}",
                endpoint,
                (int)response.StatusCode,
                string.IsNullOrWhiteSpace(responseBody) ? "(sin body)" : responseBody);

            return new UltraMsgSendResult
            {
                IsSuccess = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                ResponseBody = responseBody,
                ErrorMessage = response.IsSuccessStatusCode ? null : "UltraMsg devolvio un codigo no exitoso."
            };
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error llamando a UltraMsg.");

            return new UltraMsgSendResult
            {
                IsSuccess = false,
                ErrorMessage = exception.Message
            };
        }
    }

    private async Task<T> SendAndParseAsync<T>(
        HttpRequestMessage request,
        string endpoint,
        Func<string, T> parser,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogInformation(
            "UltraMsg {Method} {Endpoint} -> {StatusCode}\n{ResponseBody}",
            request.Method.Method,
            endpoint,
            (int)response.StatusCode,
            string.IsNullOrWhiteSpace(responseBody) ? "(sin body)" : responseBody);

        response.EnsureSuccessStatusCode();
        return parser(responseBody);
    }

    private static void CollectChatIds(JsonElement element, List<string> ids)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectChatIds(item, ids);
                }
                break;

            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.NameEquals("id") || property.NameEquals("chatId"))
                    {
                        if (property.Value.ValueKind == JsonValueKind.String)
                        {
                            ids.Add(property.Value.GetString() ?? string.Empty);
                        }
                    }
                    else
                    {
                        CollectChatIds(property.Value, ids);
                    }
                }
                break;

            case JsonValueKind.String:
                ids.Add(element.GetString() ?? string.Empty);
                break;
        }
    }

    private static void CollectMessages(JsonElement element, string fallbackChatId, List<UltraMsgChatMessage> messages)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    messages.Add(ParseMessage(item, fallbackChatId));
                }
            }

            return;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    CollectMessages(property.Value, fallbackChatId, messages);
                }
            }
        }
    }

    private static UltraMsgChatMessage ParseMessage(JsonElement element, string fallbackChatId)
    {
        return new UltraMsgChatMessage
        {
            Id = GetString(element, "id"),
            ChatId = GetString(element, "chatId") ?? fallbackChatId,
            From = GetString(element, "from"),
            To = GetString(element, "to"),
            Author = GetString(element, "author"),
            Body = GetString(element, "body") ?? GetString(element, "text"),
            Type = GetString(element, "type"),
            Timestamp = GetInt64(element, "timestamp"),
            FromMe = GetBoolean(element, "fromMe")
        };
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
    }

    private static long? GetInt64(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var number))
        {
            return number;
        }

        if (property.ValueKind == JsonValueKind.String && long.TryParse(property.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static bool? GetBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
        {
            return property.GetBoolean();
        }

        if (property.ValueKind == JsonValueKind.String && bool.TryParse(property.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }
}
