using VannaLight.Core.Models;

namespace VannaLight.Api.Services;

// Compatibilidad temporal mientras migramos referencias legacy.
public sealed class WiDocIngestor
{
    private readonly DocumentIngestor _inner;

    public WiDocIngestor(DocumentIngestor inner)
    {
        _inner = inner;
    }

    public async Task<WiReindexResult> ReindexAsync(CancellationToken ct)
    {
        var result = await _inner.ReindexAsync(ct);
        return new WiReindexResult(result.TotalFiles, result.Indexed, result.Skipped, result.Errors);
    }
}
