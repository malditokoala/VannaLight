using Microsoft.Extensions.Options;
using UltraMsgWebhookSpike.Models;
using UltraMsgWebhookSpike.Options;
using UltraMsgWebhookSpike.Services;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.IncludeScopes = false;
    options.SingleLine = false;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
});

builder.Services.Configure<UltraMsgOptions>(builder.Configuration.GetSection("UltraMsg"));
builder.Services.AddSingleton<RequestInspector>();
builder.Services.AddSingleton<UltraMsgPollingState>();
builder.Services.AddSingleton<UltraMsgPollingService>();
builder.Services.AddHttpClient<UltraMsgClient>();
builder.Services.AddHostedService<UltraMsgPollingWorker>();

var app = builder.Build();

app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        status = "ok",
        app = "UltraMsgWebhookSpike",
        utcNow = DateTimeOffset.UtcNow,
        pollingEnabled = builder.Configuration.GetValue<bool>("UltraMsg:EnablePollingForTests")
    });
});

app.MapPost("/polling/run-once", async (
    UltraMsgPollingService pollingService,
    CancellationToken cancellationToken) =>
{
    var result = await pollingService.RunOnceAsync(cancellationToken);
    return Results.Ok(result);
});

app.MapPost("/webhook/whatsapp", async (
    HttpContext httpContext,
    RequestInspector requestInspector,
    UltraMsgClient ultraMsgClient,
    IOptions<UltraMsgOptions> ultraMsgOptions,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    var logger = loggerFactory.CreateLogger("Webhook.WhatsApp");
    var inspection = await requestInspector.InspectAsync(httpContext.Request, cancellationToken);

    logger.LogInformation("Webhook POST /webhook/whatsapp recibido");
    logger.LogInformation("Content-Type: {ContentType}", inspection.ContentType ?? "(sin content-type)");
    logger.LogInformation("Headers:\n{Headers}", inspection.HeadersJson);
    logger.LogInformation("Query params:\n{Query}", inspection.QueryJson);
    logger.LogInformation("Form fields:\n{Form}", inspection.FormJson);
    logger.LogInformation("Body raw:\n{RawBody}", string.IsNullOrWhiteSpace(inspection.RawBody) ? "(vacio)" : inspection.RawBody);
    logger.LogInformation("Body parseado:\n{ParsedBody}", inspection.ParsedBodyJson ?? "(no se pudo parsear)");

    var options = ultraMsgOptions.Value;
    UltraMsgSendResult? sendResult = null;
    string? replyText = null;
    string? skipReason = null;

    if (inspection.Message.FromMe == true)
    {
        skipReason = "Mensaje ignorado porque parece provenir de la misma instancia (fromMe=true).";
    }
    else if (!inspection.Message.HasText)
    {
        skipReason = "No se detecto texto util del usuario en el payload.";
    }
    else if (string.IsNullOrWhiteSpace(inspection.Message.From))
    {
        skipReason = "No se detecto el remitente.";
    }
    else if (!options.EnableReply)
    {
        skipReason = "La respuesta automatica esta deshabilitada en configuracion.";
    }
    else if (string.IsNullOrWhiteSpace(options.InstanceId))
    {
        skipReason = "Falta configurar UltraMsg:InstanceId.";
    }
    else if (string.IsNullOrWhiteSpace(options.Token))
    {
        skipReason = "Falta configurar UltraMsg:Token.";
    }
    else
    {
        replyText = $"{options.FixedReplyPrefix}{inspection.Message.Text}";

        sendResult = await ultraMsgClient.SendTextMessageAsync(
            to: inspection.Message.From!,
            body: replyText,
            replyToMessageId: options.IncludeReplyToMessageId ? inspection.Message.MessageId : null,
            cancellationToken: cancellationToken);

        if (!sendResult.IsSuccess)
        {
            skipReason = "UltraMsg respondio con error al intentar enviar la respuesta.";
        }
    }

    var result = new WebhookProcessResult
    {
        ReceivedAtUtc = DateTimeOffset.UtcNow,
        EventType = inspection.Message.EventType,
        From = inspection.Message.From,
        Text = inspection.Message.Text,
        MessageId = inspection.Message.MessageId,
        ChatId = inspection.Message.ChatId,
        FromMe = inspection.Message.FromMe,
        HasText = inspection.Message.HasText,
        ReplyAttempted = sendResult is not null,
        ReplyText = replyText,
        ReplySkippedReason = skipReason,
        UltraMsgResponse = sendResult
    };

    logger.LogInformation("Extraccion:\n{Extraction}", inspection.MessageJson);
    logger.LogInformation("Resultado:\n{Result}", requestInspector.Serialize(result));

    return Results.Ok(result);
});

app.Run();
