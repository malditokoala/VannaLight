using Dapper;
using Microsoft.Data.Sqlite;
using VannaLight.Core.Abstractions;

namespace VannaLight.Infrastructure.Data;

public class SqliteDocChunkRepository : IDocChunkRepository
{
    public async Task<IEnumerable<DocChunkDto>> GetRecentChunksByDomainAsync(string sqlitePath, string domain, int limit, CancellationToken ct)
    {
        using var conn = new SqliteConnection($"Data Source={sqlitePath}");
        await conn.OpenAsync(ct);

        var sql = @"
            SELECT
                d.DocId,
                d.FileName,
                d.FilePath,
                CAST(c.PageNumber AS INTEGER) AS PageNumber,
                c.Text,
                CAST(COALESCE(c.ChunkOrder, 1) AS INTEGER) AS ChunkOrder,
                c.ChunkTitle,
                c.SectionName,
                c.PartNumbers,
                c.NormalizedTokens,
                CAST(COALESCE(c.TokenCount, 0) AS INTEGER) AS TokenCount,
                CAST(COALESCE(c.IsCoverPage, 0) AS INTEGER) AS IsCoverPage
            FROM DocChunks c
            JOIN DocDocuments d ON d.DocId = c.DocId
            WHERE d.Domain = @Domain
            ORDER BY d.UpdatedUtc DESC, c.PageNumber ASC, COALESCE(c.ChunkOrder, 1) ASC
            LIMIT @Limit;";

        return await conn.QueryAsync<DocChunkDto>(sql, new { Domain = domain, Limit = limit });
    }

    public async Task<IEnumerable<string>> GetPageTextPartsAsync(string sqlitePath, string docId, int pageNumber, CancellationToken ct)
    {
        using var conn = new SqliteConnection($"Data Source={sqlitePath}");
        await conn.OpenAsync(ct);

        var sql = @"
            SELECT Text
            FROM DocChunks
            WHERE DocId = @DocId AND PageNumber = @PageNumber
            ORDER BY COALESCE(ChunkOrder, 1) ASC, rowid ASC;";

        return await conn.QueryAsync<string>(sql, new { DocId = docId, PageNumber = pageNumber });
    }

    public async Task<IEnumerable<DocumentSummaryDto>> GetDocumentsByDomainAsync(string sqlitePath, string? domain, int limit, CancellationToken ct)
    {
        using var conn = new SqliteConnection($"Data Source={sqlitePath}");
        await conn.OpenAsync(ct);

        var sql = @"
            SELECT
                d.DocId,
                d.Domain,
                d.FileName,
                d.FilePath,
                d.Title,
                d.DocumentType,
                CAST(COALESCE(d.PageCount, 0) AS INTEGER) AS PageCount,
                CAST(COUNT(c.Id) AS INTEGER) AS ChunkCount,
                CAST(d.UpdatedUtc AS TEXT) AS UpdatedUtc
            FROM DocDocuments d
            LEFT JOIN DocChunks c ON c.DocId = d.DocId
            WHERE (@Domain IS NULL OR @Domain = '' OR d.Domain = @Domain)
            GROUP BY d.DocId, d.Domain, d.FileName, d.FilePath, d.Title, d.DocumentType, d.PageCount, d.UpdatedUtc
            ORDER BY d.UpdatedUtc DESC
            LIMIT @Limit;";

        return await conn.QueryAsync<DocumentSummaryDto>(sql, new { Domain = domain, Limit = limit });
    }

    public async Task<IEnumerable<DocumentChunkAdminDto>> GetDocumentChunksAsync(string sqlitePath, string docId, CancellationToken ct)
    {
        using var conn = new SqliteConnection($"Data Source={sqlitePath}");
        await conn.OpenAsync(ct);

        var sql = @"
            SELECT
                COALESCE(ChunkKey, printf('%s:%d:%d', DocId, PageNumber, COALESCE(ChunkOrder, 1))) AS ChunkKey,
                CAST(PageNumber AS INTEGER) AS PageNumber,
                CAST(COALESCE(ChunkOrder, 1) AS INTEGER) AS ChunkOrder,
                ChunkTitle,
                SectionName,
                PartNumbers,
                CAST(COALESCE(TokenCount, 0) AS INTEGER) AS TokenCount,
                CAST(COALESCE(IsCoverPage, 0) AS INTEGER) AS IsCoverPage,
                Text
            FROM DocChunks
            WHERE DocId = @DocId
            ORDER BY PageNumber ASC, COALESCE(ChunkOrder, 1) ASC, rowid ASC;";

        return await conn.QueryAsync<DocumentChunkAdminDto>(sql, new { DocId = docId });
    }
}
