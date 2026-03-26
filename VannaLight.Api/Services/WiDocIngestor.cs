using Dapper;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;
using UglyToad.PdfPig;
using VannaLight.Api.Contracts;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;
using VannaLight.Core.Settings;

namespace VannaLight.Api.Services;

public sealed class WiDocIngestor
{
    private readonly ISystemConfigProvider _systemConfigProvider;
    private readonly IHostEnvironment _environment;
    private readonly SqliteOptions _sqliteOptions;

    public WiDocIngestor(
        ISystemConfigProvider systemConfigProvider,
        IHostEnvironment environment,
        SqliteOptions sqliteOptions)
    {
        _systemConfigProvider = systemConfigProvider;
        _environment = environment;
        _sqliteOptions = sqliteOptions;
    }

    public async Task<WiReindexResult> ReindexAsync(CancellationToken ct)
    {
        string wiRootSetting = await _systemConfigProvider.GetRequiredValueAsync("Docs", "WiRootPath", ct);
        string wiRoot = Path.IsPathRooted(wiRootSetting)
            ? wiRootSetting
            : Path.GetFullPath(Path.Combine(_environment.ContentRootPath, wiRootSetting));
        string sqlitePath = _sqliteOptions.DbPath;

        if (!Directory.Exists(wiRoot))
            throw new DirectoryNotFoundException($"No existe Docs:WiRootPath: {wiRoot}");

        var files = Directory.EnumerateFiles(wiRoot, "*.pdf", SearchOption.TopDirectoryOnly).ToList();
        int total = files.Count;
        int indexed = 0, skipped = 0, errors = 0;

        await using var conn = new SqliteConnection($"Data Source={sqlitePath}");
        await conn.OpenAsync(ct);

        await EnsureTablesAsync(conn);

        foreach (var filePath in files)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                string sha256 = ComputeSha256Hex(filePath);
                string fileName = Path.GetFileName(filePath);

                var existingSha = await conn.QueryFirstOrDefaultAsync<string?>(
                    "SELECT Sha256 FROM DocDocuments WHERE FilePath = @FilePath LIMIT 1;",
                    new { FilePath = filePath }
                );

                if (!string.IsNullOrEmpty(existingSha) &&
                    string.Equals(existingSha, sha256, StringComparison.OrdinalIgnoreCase))
                {
                    skipped++;
                    continue;
                }

                string docId = ComputeSha256HexFromString(filePath.ToLowerInvariant());

                await conn.ExecuteAsync(@"
                                        INSERT INTO DocDocuments (DocId, FileName, FilePath, Domain, Sha256, UpdatedUtc)
                                        VALUES (@DocId, @FileName, @FilePath, @Domain, @Sha256, @UpdatedUtc)
                                        ON CONFLICT(DocId) DO UPDATE SET
                                          FileName = excluded.FileName,
                                          FilePath = excluded.FilePath,
                                          Domain = excluded.Domain,
                                          Sha256 = excluded.Sha256,
                                          UpdatedUtc = excluded.UpdatedUtc;",
                    new
                    {
                        DocId = docId,
                        FileName = fileName,
                        FilePath = filePath,
                        Domain = "work-instructions",
                        Sha256 = sha256,
                        UpdatedUtc = DateTime.UtcNow.ToString("o")
                    }
                 );

                    await conn.ExecuteAsync(
                    "DELETE FROM DocChunks WHERE DocId = @DocId",
                    new { DocId = docId });

                var pages = ExtractPdfTextByPage(filePath);

                using var tx = conn.BeginTransaction();
                int pageNo = 0;

                foreach (var pageText in pages)
                {
                    pageNo++;
                    var text = Sanitize(pageText);

                    await conn.ExecuteAsync(@"
                                            INSERT INTO DocChunks (ChunkId, DocId, PageNumber, Text, UpdatedUtc)
                                            VALUES (@ChunkId, @DocId, @PageNumber, @Text, @UpdatedUtc);",
                        new
                        {
                            ChunkId = $"{docId}:{pageNo}",
                            DocId = docId,
                            PageNumber = pageNo,
                            Text = text,
                            UpdatedUtc = DateTime.UtcNow.ToString("o")
                        }, tx
                        
                    );
                }

                tx.Commit();
                indexed++;
            }
            catch
            {
                errors++;
            }
        }

        return new WiReindexResult(total, indexed, skipped, errors);
    }

    private static async Task EnsureTablesAsync(SqliteConnection conn)
    {
        await conn.ExecuteAsync(@"
                                CREATE TABLE IF NOT EXISTS DocDocuments (
                                  DocId TEXT PRIMARY KEY,
                                  FileName TEXT NOT NULL,
                                  FilePath TEXT NOT NULL,
                                  Domain TEXT NOT NULL,
                                  Sha256 TEXT NOT NULL,
                                  UpdatedUtc TEXT NOT NULL
                                );"
        );

        await conn.ExecuteAsync(@"
                                CREATE TABLE IF NOT EXISTS DocChunks (
                                  ChunkId TEXT PRIMARY KEY,
                                  DocId TEXT NOT NULL,
                                  PageNumber INTEGER NOT NULL,
                                  Text TEXT NOT NULL,
                                  UpdatedUtc TEXT NOT NULL,
                                  FOREIGN KEY (DocId) REFERENCES DocDocuments(DocId)
                                );"
        );

        await conn.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_DocChunks_DocId ON DocChunks(DocId);");
        await conn.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_DocChunks_PageNumber ON DocChunks(PageNumber);");
    }

    private static IEnumerable<string> ExtractPdfTextByPage(string filePath)
    {
        using var doc = PdfDocument.Open(filePath);
        foreach (var page in doc.GetPages())
            yield return page.Text;
    }

    private static string Sanitize(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\0", "").Replace("\uFFFF", "").Trim();
    }

    private static string ComputeSha256Hex(string filePath)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    private static string ComputeSha256HexFromString(string input)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}
