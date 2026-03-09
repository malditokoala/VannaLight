using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace VannaLight.Core.Abstractions;

// Reemplazamos el "DocChunkRow" privado de tu servicio con un DTO oficial del Core
public record DocChunkDto(string DocId, string FileName, long PageNumber, string Text);

public interface IDocChunkRepository
{
    // Extrae los fragmentos más recientes de un dominio (reemplaza tu primer query)
    Task<IEnumerable<DocChunkDto>> GetRecentChunksByDomainAsync(string sqlitePath, string domain, int limit, CancellationToken ct);

    // Extrae las partes de texto de una página y documento específico (reemplaza GetPageTextAsync)
    Task<IEnumerable<string>> GetPageTextPartsAsync(string sqlitePath, string docId, int pageNumber, CancellationToken ct);
}