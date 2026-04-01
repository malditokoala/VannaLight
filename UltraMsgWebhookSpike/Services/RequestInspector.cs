using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using UltraMsgWebhookSpike.Models;

namespace UltraMsgWebhookSpike.Services;

public sealed class RequestInspector
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public async Task<RequestInspectionResult> InspectAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        request.EnableBuffering();

        string rawBody;
        using (var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
        {
            rawBody = await reader.ReadToEndAsync(cancellationToken);
        }

        request.Body.Position = 0;

        var headers = request.Headers.ToDictionary(
            pair => pair.Key,
            pair => ToCleanArray(pair.Value),
            StringComparer.OrdinalIgnoreCase);

        var query = request.Query.ToDictionary(
            pair => pair.Key,
            pair => ToCleanArray(pair.Value),
            StringComparer.OrdinalIgnoreCase);

        Dictionary<string, string[]> form = new(StringComparer.OrdinalIgnoreCase);
        if (request.HasFormContentType)
        {
            var formCollection = await request.ReadFormAsync(cancellationToken);
            form = formCollection.ToDictionary(
                pair => pair.Key,
                pair => ToCleanArray(pair.Value),
                StringComparer.OrdinalIgnoreCase);
            request.Body.Position = 0;
        }

        string? parsedBodyJson = null;
        Dictionary<string, string> flattenedBodyValues = new(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(rawBody))
        {
            if (LooksLikeJson(rawBody))
            {
                try
                {
                    using var document = JsonDocument.Parse(rawBody);
                    parsedBodyJson = JsonSerializer.Serialize(document.RootElement, JsonOptions);
                    FlattenJson(document.RootElement, string.Empty, flattenedBodyValues);
                }
                catch
                {
                    parsedBodyJson = null;
                }
            }
            else if (request.ContentType?.Contains("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) == true)
            {
                var parsedForm = QueryHelpers.ParseQuery(rawBody);
                foreach (var item in parsedForm)
                {
                    flattenedBodyValues[item.Key] = item.Value.ToString();
                }

                parsedBodyJson = JsonSerializer.Serialize(
                    parsedForm.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()),
                    JsonOptions);
            }
        }

        var message = ExtractMessage(flattenedBodyValues, form, query);

        return new RequestInspectionResult
        {
            ContentType = request.ContentType,
            Headers = headers,
            Query = query,
            Form = form,
            RawBody = rawBody,
            ParsedBodyJson = parsedBodyJson,
            FlattenedBodyValues = flattenedBodyValues,
            Message = message,
            HeadersJson = Serialize(headers),
            QueryJson = Serialize(query),
            FormJson = Serialize(form),
            MessageJson = Serialize(message)
        };
    }

    public string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, JsonOptions);
    }

    private static ReceivedMessageInfo ExtractMessage(
        IReadOnlyDictionary<string, string> bodyValues,
        IReadOnlyDictionary<string, string[]> formValues,
        IReadOnlyDictionary<string, string[]> queryValues)
    {
        string? Find(params string[] keys)
        {
            foreach (var key in keys)
            {
                if (bodyValues.TryGetValue(key, out var bodyValue) && !string.IsNullOrWhiteSpace(bodyValue))
                {
                    return bodyValue;
                }

                if (TryGetStringArrayValue(formValues, key, out var formValue))
                {
                    return formValue;
                }

                if (TryGetStringArrayValue(queryValues, key, out var queryValue))
                {
                    return queryValue;
                }
            }

            return null;
        }

        bool? FindBool(params string[] keys)
        {
            var rawValue = Find(keys);
            if (bool.TryParse(rawValue, out var boolValue))
            {
                return boolValue;
            }

            return null;
        }

        return new ReceivedMessageInfo
        {
            EventType = Find("event_type", "eventType", "type", "data.event_type"),
            From = NormalizePhoneOrChatId(Find("data.from", "from", "data.author", "author", "chatId", "data.chatId")),
            Text = Find(
                "data.body",
                "body",
                "data.text",
                "text",
                "message",
                "data.message",
                "data.message.body",
                "data.message.text",
                "Body",
                "question",
                "prompt"),
            MessageId = Find("data.id", "id", "msgId", "messageId", "data.message.id"),
            ChatId = Find("data.chatId", "chatId", "data.from", "from"),
            FromMe = FindBool("data.fromMe", "fromMe")
        };
    }

    private static string? NormalizePhoneOrChatId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value.Trim();
    }

    private static bool TryGetStringArrayValue(IReadOnlyDictionary<string, string[]> values, string key, out string? result)
    {
        if (values.TryGetValue(key, out var matches))
        {
            var first = matches.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            if (!string.IsNullOrWhiteSpace(first))
            {
                result = first;
                return true;
            }
        }

        result = null;
        return false;
    }

    private static bool LooksLikeJson(string rawBody)
    {
        var trimmed = rawBody.TrimStart();
        return trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal);
    }

    private static string[] ToCleanArray(IEnumerable<string?> values)
    {
        return values
            .Where(value => value is not null)
            .Select(value => value!)
            .ToArray();
    }

    private static void FlattenJson(JsonElement element, string prefix, Dictionary<string, string> values)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var propertyPath = string.IsNullOrWhiteSpace(prefix)
                        ? property.Name
                        : $"{prefix}.{property.Name}";

                    FlattenJson(property.Value, propertyPath, values);
                }
                break;

            case JsonValueKind.Array:
                values[prefix] = JsonSerializer.Serialize(element);
                for (var index = 0; index < element.GetArrayLength(); index++)
                {
                    var itemPath = $"{prefix}[{index}]";
                    FlattenJson(element[index], itemPath, values);
                }
                break;

            case JsonValueKind.String:
                values[prefix] = element.GetString() ?? string.Empty;
                break;

            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                values[prefix] = element.ToString();
                break;

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                values[prefix] = string.Empty;
                break;
        }
    }
}
