using Dapper;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;
using VannaLight.Core.Settings;

namespace VannaLight.Api.Services;

public sealed class DocumentIngestor
{
    private static readonly Regex PartNumberRegex = new(@"\b\d{4,}-\d{3,}\b", RegexOptions.Compiled);
    private static readonly Regex TokenRegex = new(@"[a-z0-9][a-z0-9\-_/]{2,}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ISystemConfigProvider _systemConfigProvider;
    private readonly IHostEnvironment _environment;
    private readonly SqliteOptions _sqliteOptions;
    private readonly ILogger<DocumentIngestor> _logger;

    public DocumentIngestor(
        ISystemConfigProvider systemConfigProvider,
        IHostEnvironment environment,
        SqliteOptions sqliteOptions,
        ILogger<DocumentIngestor> logger)
    {
        _systemConfigProvider = systemConfigProvider;
        _environment = environment;
        _sqliteOptions = sqliteOptions;
        _logger = logger;
    }

    public async Task<DocumentReindexResult> ReindexAsync(CancellationToken ct)
    {
        string docsRoot = await ResolveDocumentsRootPathAsync(ct);
        string domain = await ResolveDocumentsDomainAsync(ct);
        string sqlitePath = _sqliteOptions.DbPath;

        if (!Directory.Exists(docsRoot))
            throw new DirectoryNotFoundException($"No existe la carpeta configurada de documentos: {docsRoot}");

        var files = Directory.EnumerateFiles(docsRoot, "*.pdf", SearchOption.AllDirectories).ToList();
        int total = files.Count;
        int indexed = 0;
        int skipped = 0;
        int errors = 0;

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
                string docId = NormalizeDocId(filePath);

                var existingSha = await conn.QueryFirstOrDefaultAsync<string?>(
                    "SELECT Sha256 FROM DocDocuments WHERE FilePath = @FilePath LIMIT 1;",
                    new { FilePath = filePath });

                if (!string.IsNullOrWhiteSpace(existingSha) && string.Equals(existingSha, sha256, StringComparison.OrdinalIgnoreCase))
                {
                    skipped++;
                    continue;
                }

                var pages = ExtractPdfTextByPage(filePath).ToList();
                var title = DetectDocumentTitle(pages);
                var pageCount = pages.Count;

                await conn.ExecuteAsync(@"
                    INSERT INTO DocDocuments (DocId, FileName, FilePath, Domain, Sha256, UpdatedUtc, PageCount, DocumentType, Title)
                    VALUES (@DocId, @FileName, @FilePath, @Domain, @Sha256, @UpdatedUtc, @PageCount, @DocumentType, @Title)
                    ON CONFLICT(DocId) DO UPDATE SET
                        FileName = excluded.FileName,
                        FilePath = excluded.FilePath,
                        Domain = excluded.Domain,
                        Sha256 = excluded.Sha256,
                        UpdatedUtc = excluded.UpdatedUtc,
                        PageCount = excluded.PageCount,
                        DocumentType = excluded.DocumentType,
                        Title = excluded.Title;",
                    new
                    {
                        DocId = docId,
                        FileName = fileName,
                        FilePath = filePath,
                        Domain = domain,
                        Sha256 = sha256,
                        UpdatedUtc = DateTime.UtcNow.ToString("o"),
                        PageCount = pageCount,
                        DocumentType = "pdf",
                        Title = title
                    });

                await conn.ExecuteAsync("DELETE FROM DocChunks WHERE DocId = @DocId;", new { DocId = docId });

                using var tx = conn.BeginTransaction();
                foreach (var page in BuildPageChunks(docId, pages))
                {
                    foreach (var chunk in page)
                    {
                        await conn.ExecuteAsync(@"
                            INSERT INTO DocChunks (
                                ChunkKey,
                                DocId,
                                PageNumber,
                                ChunkOrder,
                                ChunkTitle,
                                SectionName,
                                PartNumbers,
                                NormalizedTokens,
                                TokenCount,
                                IsCoverPage,
                                Text,
                                UpdatedUtc)
                            VALUES (
                                @ChunkKey,
                                @DocId,
                                @PageNumber,
                                @ChunkOrder,
                                @ChunkTitle,
                                @SectionName,
                                @PartNumbers,
                                @NormalizedTokens,
                                @TokenCount,
                                @IsCoverPage,
                                @Text,
                                @UpdatedUtc);",
                            chunk,
                            tx);
                    }
                }

                tx.Commit();
                indexed++;
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogWarning(ex, "No se pudo indexar el documento {FilePath}", filePath);
            }
        }

        return new DocumentReindexResult(total, indexed, skipped, errors);
    }

    public async Task<string> ResolveDocumentsRootPathAsync(CancellationToken ct)
    {
        var configured = (await _systemConfigProvider.GetValueAsync("Docs", "RootPath", ct: ct))?.Trim();
        configured ??= (await _systemConfigProvider.GetValueAsync("Docs", "WiRootPath", ct: ct))?.Trim();
        configured ??= "Data/docs";

        return Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(_environment.ContentRootPath, configured));
    }

    public async Task<string> ResolveDocumentsDomainAsync(CancellationToken ct)
    {
        var configured = (await _systemConfigProvider.GetValueAsync("Docs", "DefaultDomain", ct: ct))?.Trim();
        return string.IsNullOrWhiteSpace(configured) ? "work-instructions" : configured;
    }

    private static async Task EnsureTablesAsync(SqliteConnection conn)
    {
        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS DocDocuments (
                DocId TEXT PRIMARY KEY,
                Domain TEXT NOT NULL,
                FileName TEXT NOT NULL,
                FilePath TEXT NULL,
                Sha256 TEXT NULL,
                PageCount INTEGER NOT NULL DEFAULT 0,
                DocumentType TEXT NULL,
                Title TEXT NULL,
                UpdatedUtc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS DocChunks (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ChunkKey TEXT NULL,
                DocId TEXT NOT NULL,
                PageNumber INTEGER NOT NULL,
                ChunkOrder INTEGER NOT NULL DEFAULT 1,
                ChunkTitle TEXT NULL,
                SectionName TEXT NULL,
                PartNumbers TEXT NULL,
                NormalizedTokens TEXT NULL,
                TokenCount INTEGER NOT NULL DEFAULT 0,
                IsCoverPage INTEGER NOT NULL DEFAULT 0,
                Text TEXT NOT NULL,
                UpdatedUtc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (DocId) REFERENCES DocDocuments(DocId)
            );

            CREATE UNIQUE INDEX IF NOT EXISTS IX_DocChunks_ChunkKey ON DocChunks(ChunkKey);
            CREATE INDEX IF NOT EXISTS IX_DocChunks_DocId_PageNumber_Order ON DocChunks(DocId, PageNumber, ChunkOrder);
            CREATE INDEX IF NOT EXISTS IX_DocDocuments_Domain_UpdatedUtc ON DocDocuments(Domain, UpdatedUtc DESC);
        ");
    }

    private static List<List<DocumentChunkRow>> BuildPageChunks(string docId, List<string> pages)
    {
        var result = new List<List<DocumentChunkRow>>();

        for (var i = 0; i < pages.Count; i++)
        {
            var pageNumber = i + 1;
            var pageText = Sanitize(pages[i]);
            var pageTitle = DetectChunkTitle(pageText);
            var chunkTexts = CreateChunkWindows(pageText, 900, 180);
            var pageChunks = new List<DocumentChunkRow>();

            for (var chunkIndex = 0; chunkIndex < chunkTexts.Count; chunkIndex++)
            {
                var chunkText = chunkTexts[chunkIndex];
                var section = DetectSectionName(chunkText, pageTitle);
                var partNumbers = string.Join(", ", ExtractPartNumbers(chunkText));
                var tokens = ExtractNormalizedTokens(chunkText);

                pageChunks.Add(new DocumentChunkRow
                {
                    ChunkKey = $"{docId}:{pageNumber}:{chunkIndex + 1}",
                    DocId = docId,
                    PageNumber = pageNumber,
                    ChunkOrder = chunkIndex + 1,
                    ChunkTitle = pageTitle,
                    SectionName = section,
                    PartNumbers = string.IsNullOrWhiteSpace(partNumbers) ? null : partNumbers,
                    NormalizedTokens = tokens.Count == 0 ? null : string.Join(' ', tokens),
                    TokenCount = tokens.Count,
                    IsCoverPage = pageNumber == 1,
                    Text = chunkText,
                    UpdatedUtc = DateTime.UtcNow.ToString("o")
                });
            }

            result.Add(pageChunks);
        }

        return result;
    }

    private static List<string> CreateChunkWindows(string text, int targetSize, int overlap)
    {
        var clean = Sanitize(text);
        if (string.IsNullOrWhiteSpace(clean))
            return new List<string>();

        var paragraphs = clean
            .Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        if (paragraphs.Count <= 1)
            paragraphs = clean
                .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

        if (paragraphs.Count == 0)
            paragraphs.Add(clean);

        var chunks = new List<string>();
        var current = new StringBuilder();
        foreach (var paragraph in paragraphs)
        {
            var candidateLength = current.Length == 0 ? paragraph.Length : current.Length + 2 + paragraph.Length;
            if (current.Length > 0 && candidateLength > targetSize)
            {
                chunks.Add(current.ToString().Trim());
                var tail = current.Length > overlap ? current.ToString()[Math.Max(0, current.Length - overlap)..] : current.ToString();
                current.Clear();
                if (!string.IsNullOrWhiteSpace(tail))
                    current.Append(tail.Trim());
            }

            if (current.Length > 0)
                current.AppendLine().AppendLine();
            current.Append(paragraph.Trim());
        }

        if (current.Length > 0)
            chunks.Add(current.ToString().Trim());

        return chunks
            .Select(Sanitize)
            .Where(chunk => !string.IsNullOrWhiteSpace(chunk))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static IEnumerable<string> ExtractPdfTextByPage(string filePath)
    {
        using var doc = PdfDocument.Open(filePath);
        foreach (var page in doc.GetPages())
            yield return page.Text;
    }

    private static string DetectDocumentTitle(IEnumerable<string> pages)
    {
        foreach (var page in pages.Take(2))
        {
            var title = DetectChunkTitle(page);
            if (!string.IsNullOrWhiteSpace(title))
                return title;
        }

        return string.Empty;
    }

    private static string DetectChunkTitle(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var line = text
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(l => l.Length is > 5 and < 120);

        return Sanitize(line);
    }

    private static string? DetectSectionName(string text, string? fallbackTitle)
    {
        var lines = text
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(8)
            .ToList();

        foreach (var line in lines)
        {
            if (line.Length is < 4 or > 80)
                continue;

            if (line.StartsWith("section", StringComparison.OrdinalIgnoreCase)
                || line.All(ch => !char.IsLetter(ch) || char.IsUpper(ch) || char.IsWhiteSpace(ch) || ch == '-' || ch == ':'))
            {
                return Sanitize(line);
            }
        }

        return string.IsNullOrWhiteSpace(fallbackTitle) ? null : Sanitize(fallbackTitle);
    }

    private static List<string> ExtractPartNumbers(string text)
        => PartNumberRegex.Matches(text ?? string.Empty)
            .Select(m => m.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<string> ExtractNormalizedTokens(string text)
        => TokenRegex.Matches((text ?? string.Empty).ToLowerInvariant())
            .Select(m => m.Value)
            .Where(token => token.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(32)
            .ToList();

    private static string Sanitize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return string.Empty;

        var normalized = s.Replace("\0", string.Empty)
            .Replace("\uFFFF", string.Empty)
            .Replace('\t', ' ');

        normalized = Regex.Replace(normalized, @"\s{2,}", " ");
        normalized = Regex.Replace(normalized, @"(\r?\n){3,}", Environment.NewLine + Environment.NewLine);
        return normalized.Trim();
    }

    private static string ComputeSha256Hex(string filePath)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    private static string NormalizeDocId(string input)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input.ToLowerInvariant());
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }

    private sealed class DocumentChunkRow
    {
        public string ChunkKey { get; init; } = string.Empty;
        public string DocId { get; init; } = string.Empty;
        public int PageNumber { get; init; }
        public int ChunkOrder { get; init; }
        public string? ChunkTitle { get; init; }
        public string? SectionName { get; init; }
        public string? PartNumbers { get; init; }
        public string? NormalizedTokens { get; init; }
        public int TokenCount { get; init; }
        public bool IsCoverPage { get; init; }
        public string Text { get; init; } = string.Empty;
        public string UpdatedUtc { get; init; } = string.Empty;
    }
}
