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
            SELECT d.DocId, d.FileName, c.PageNumber, c.Text
            FROM DocChunks c
            JOIN DocDocuments d ON d.DocId = c.DocId
            WHERE d.Domain = @Domain
            ORDER BY d.UpdatedUtc DESC
            LIMIT @Limit;";

        // Ejecutamos el query y Dapper mapea automáticamente las columnas al record DocChunkDto
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
            ORDER BY rowid ASC;";

        // Aquí solo traemos una lista de strings (el texto crudo de la página)
        return await conn.QueryAsync<string>(sql, new { DocId = docId, PageNumber = pageNumber });
    }
}