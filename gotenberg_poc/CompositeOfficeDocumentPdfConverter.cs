using Microsoft.Extensions.Logging;

namespace QrPrensas.Services;

public sealed class CompositeOfficeDocumentPdfConverter : IOfficeDocumentPdfConverter
{
    private readonly GotenbergDocumentPdfConverter _gotenbergConverter;
    private readonly LibreOfficeDocumentPdfConverter _libreOfficeConverter;
    private readonly ILogger<CompositeOfficeDocumentPdfConverter> _logger;

    public CompositeOfficeDocumentPdfConverter(
        GotenbergDocumentPdfConverter gotenbergConverter,
        LibreOfficeDocumentPdfConverter libreOfficeConverter,
        ILogger<CompositeOfficeDocumentPdfConverter> logger)
    {
        _gotenbergConverter = gotenbergConverter;
        _libreOfficeConverter = libreOfficeConverter;
        _logger = logger;
    }

    public async Task<byte[]> TryConvertToPdfAsync(
        string sourceFileName,
        byte[] sourceContent,
        CancellationToken cancellationToken = default)
    {
        var gotenbergPdf = await _gotenbergConverter.TryConvertToPdfAsync(sourceFileName, sourceContent, cancellationToken);
        if (gotenbergPdf != null && gotenbergPdf.Length > 0)
        {
            _logger.LogInformation("Preview PDF resuelto con Gotenberg para {FileName}.", sourceFileName);
            return gotenbergPdf;
        }

        var libreOfficePdf = await _libreOfficeConverter.TryConvertToPdfAsync(sourceFileName, sourceContent, cancellationToken);
        if (libreOfficePdf != null && libreOfficePdf.Length > 0)
        {
            _logger.LogInformation("Preview PDF resuelto con LibreOffice local para {FileName}.", sourceFileName);
            return libreOfficePdf;
        }

        _logger.LogInformation("No fue posible convertir localmente {FileName}; se dejara que Graph maneje el fallback.", sourceFileName);
        return null;
    }
}
