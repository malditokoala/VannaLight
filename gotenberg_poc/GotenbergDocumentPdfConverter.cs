using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QrPrensas.Models;

namespace QrPrensas.Services;

public sealed class GotenbergDocumentPdfConverter : IOfficeDocumentPdfConverter
{
    private static readonly HttpClient HttpClient = new HttpClient();

    private readonly GraphVisualAidOptions _options;
    private readonly ILogger<GotenbergDocumentPdfConverter> _logger;

    public GotenbergDocumentPdfConverter(
        IOptions<GraphVisualAidOptions> options,
        ILogger<GotenbergDocumentPdfConverter> logger)
    {
        _options = options.Value ?? new GraphVisualAidOptions();
        _logger = logger;
    }

    public async Task<byte[]> TryConvertToPdfAsync(
        string sourceFileName,
        byte[] sourceContent,
        CancellationToken cancellationToken = default)
    {
        if (!_options.PreferGotenbergConversion ||
            string.IsNullOrWhiteSpace(_options.GotenbergBaseUrl) ||
            string.IsNullOrWhiteSpace(sourceFileName) ||
            sourceContent == null ||
            sourceContent.Length == 0)
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                BuildGotenbergRoute());

            if (!string.IsNullOrWhiteSpace(_options.GotenbergTraceHeader))
                request.Headers.TryAddWithoutValidation("Gotenberg-Trace", _options.GotenbergTraceHeader);

            using var form = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(sourceContent);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
            form.Add(fileContent, "files", sourceFileName);

            if (IsExcelFile(sourceFileName) && _options.GotenbergSinglePageSheets)
                form.Add(new StringContent("true"), "singlePageSheets");

            request.Content = form;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(10, _options.GotenbergTimeoutSeconds)));

            using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning(
                    "Gotenberg no pudo convertir {FileName}. StatusCode={StatusCode}. Body={Body}",
                    sourceFileName,
                    (int)response.StatusCode,
                    body);
                return null;
            }

            var pdf = await response.Content.ReadAsByteArrayAsync();
            if (pdf == null || pdf.Length == 0)
            {
                _logger.LogWarning("Gotenberg devolvio un PDF vacio para {FileName}.", sourceFileName);
                return null;
            }

            _logger.LogInformation(
                "Gotenberg convirtio {FileName} a PDF. Bytes={Bytes}",
                sourceFileName,
                pdf.Length);

            return pdf;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Gotenberg excedio el timeout de conversion para {FileName}. TimeoutSeconds={TimeoutSeconds}",
                sourceFileName,
                _options.GotenbergTimeoutSeconds);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo la conversion via Gotenberg para {FileName}.", sourceFileName);
            return null;
        }
    }

    private string BuildGotenbergRoute()
    {
        return _options.GotenbergBaseUrl.TrimEnd('/') + "/forms/libreoffice/convert";
    }

    private static bool IsExcelFile(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".xls", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".xlsm", StringComparison.OrdinalIgnoreCase);
    }
}
