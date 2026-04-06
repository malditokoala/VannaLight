using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace VannaLight.Core.Abstractions;

public class DocChunkDto
{
    public string DocId { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string? FilePath { get; init; }
    public long PageNumber { get; init; }
    public string Text { get; init; } = string.Empty;
    public int ChunkOrder { get; init; }
    public string? ChunkTitle { get; init; }
    public string? SectionName { get; init; }
    public string? PartNumbers { get; init; }
    public string? NormalizedTokens { get; init; }
    public int TokenCount { get; init; }
    public bool IsCoverPage { get; init; }
}

public class DocumentSummaryDto
{
    public string DocId { get; init; } = string.Empty;
    public string Domain { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string? FilePath { get; init; }
    public string? Title { get; init; }
    public string? DocumentType { get; init; }
    public long PageCount { get; init; }
    public long ChunkCount { get; init; }
    public string UpdatedUtc { get; init; } = string.Empty;
}

public class DocumentChunkAdminDto
{
    public string ChunkKey { get; init; } = string.Empty;
    public long PageNumber { get; init; }
    public int ChunkOrder { get; init; }
    public string? ChunkTitle { get; init; }
    public string? SectionName { get; init; }
    public string? PartNumbers { get; init; }
    public int TokenCount { get; init; }
    public bool IsCoverPage { get; init; }
    public string Text { get; init; } = string.Empty;
}

public interface IDocChunkRepository
{
    Task<IEnumerable<DocChunkDto>> GetRecentChunksByDomainAsync(string sqlitePath, string domain, int limit, CancellationToken ct);
    Task<IEnumerable<string>> GetPageTextPartsAsync(string sqlitePath, string docId, int pageNumber, CancellationToken ct);
    Task<IEnumerable<DocumentSummaryDto>> GetDocumentsByDomainAsync(string sqlitePath, string? domain, int limit, CancellationToken ct);
    Task<IEnumerable<DocumentChunkAdminDto>> GetDocumentChunksAsync(string sqlitePath, string docId, CancellationToken ct);
}
