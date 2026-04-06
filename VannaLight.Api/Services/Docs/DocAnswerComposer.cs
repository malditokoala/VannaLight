using System.Text.RegularExpressions;
using VannaLight.Api.Contracts;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Api.Services.Docs;

public sealed class DocAnswerComposer : IDocAnswerComposer
{
    private readonly IDocChunkRepository _repository;

    public DocAnswerComposer(IDocChunkRepository repository)
    {
        _repository = repository;
    }

    public async Task<DocsAnswerResult> ComposeAsync(
        string sqlitePath,
        DocTypeSchema schema,
        DocsIntent intent,
        IReadOnlyList<ScoredDocChunk> scoredChunks,
        IReadOnlyList<DocChunkDto> allChunks,
        IReadOnlyCollection<string> partNumbers,
        int maxCitations,
        double confidence,
        CancellationToken ct)
    {
        if (partNumbers.Count > 0)
        {
            return await ComposePartNumberAnswerAsync(sqlitePath, schema, intent, scoredChunks, allChunks, partNumbers, maxCitations, confidence, ct);
        }

        var combinedText = string.Join("\n\n", scoredChunks.Select(x => x.Chunk.Text));
        var facts = ExtractionEngine.ExtractAll(schema, combinedText);
        var citations = BuildCitations(scoredChunks, maxCitations, confidence);

        if (facts.Count == 0)
            return BuildEvidenceOnlyResult(citations, confidence);

        return new DocsAnswerResult(true, WiAnswerBuilder.Build(facts, intent, schema), citations, null, confidence);
    }

    private async Task<DocsAnswerResult> ComposePartNumberAnswerAsync(
        string sqlitePath,
        DocTypeSchema schema,
        DocsIntent intent,
        IReadOnlyList<ScoredDocChunk> scoredChunks,
        IReadOnlyList<DocChunkDto> allChunks,
        IReadOnlyCollection<string> partNumbers,
        int maxCitations,
        double confidence,
        CancellationToken ct)
    {
        var winnerDocId = ResolveWinnerDocId(scoredChunks, allChunks, partNumbers);
        var winnerChunks = scoredChunks
            .Where(s => s.Chunk.DocId == winnerDocId)
            .OrderByDescending(s => s.Score)
            .ToList();

        var page1Parts = await _repository.GetPageTextPartsAsync(sqlitePath, winnerDocId, 1, ct);
        var extractionTexts = new List<string>();
        var coverText = string.Join("\n", page1Parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        if (!string.IsNullOrWhiteSpace(coverText))
            extractionTexts.Add(coverText);

        extractionTexts.AddRange(winnerChunks
            .Select(s => s.Chunk.Text)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Take(4));

        var extractionText = string.Join("\n\n", extractionTexts.Distinct(StringComparer.Ordinal));
        var facts = ExtractionEngine.ExtractAll(schema, extractionText);
        var citations = BuildCitations(winnerChunks, maxCitations, confidence);

        if (facts.Count == 0)
            return BuildEvidenceOnlyResult(citations, confidence);

        return new DocsAnswerResult(true, WiAnswerBuilder.Build(facts, intent, schema), citations, null, confidence);
    }

    private static DocsAnswerResult BuildEvidenceOnlyResult(IReadOnlyList<DocCitation> citations, double confidence)
    {
        return new DocsAnswerResult(
            true,
            "Encontre secciones relevantes, pero no pude extraer el dato de forma completamente confiable. Revisa la evidencia mostrada.",
            citations,
            null,
            confidence);
    }

    private static string ResolveWinnerDocId(
        IReadOnlyList<ScoredDocChunk> scoredTopK,
        IReadOnlyList<DocChunkDto> allChunks,
        IReadOnlyCollection<string> partNumbers)
    {
        var pnOnly = scoredTopK
            .Where(x => ContainsAny(x.Chunk.Text, partNumbers) || ContainsAny(x.Chunk.PartNumbers, partNumbers))
            .ToList();

        if (pnOnly.Count == 0)
        {
            return allChunks
                .Where(c => ContainsAny(c.Text, partNumbers) || ContainsAny(c.PartNumbers, partNumbers))
                .GroupBy(c => c.DocId)
                .Select(g => new { DocId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .First().DocId;
        }

        return pnOnly
            .GroupBy(x => x.Chunk.DocId)
            .Select(g => new { DocId = g.Key, BestScore = g.Max(x => x.Score) })
            .OrderByDescending(x => x.BestScore)
            .First().DocId;
    }

    private static List<DocCitation> BuildCitations(IReadOnlyList<ScoredDocChunk> scoredChunks, int maxCitations, double confidence)
        => scoredChunks
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Chunk.PageNumber)
            .ThenBy(x => x.Chunk.ChunkOrder)
            .Take(maxCitations)
            .Select(x => new DocCitation(
                x.Chunk.DocId,
                x.Chunk.FileName,
                (int)x.Chunk.PageNumber,
                BuildSnippet(x.Chunk.Text),
                x.Chunk.SectionName,
                confidence))
            .Distinct()
            .ToList();

    private static string BuildSnippet(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var normalized = Regex.Replace(text, @"\s+", " ").Trim();
        return normalized.Length <= 180 ? normalized : normalized[..180].TrimEnd() + "...";
    }

    private static bool ContainsAny(string? text, IReadOnlyCollection<string> needles)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        foreach (var needle in needles)
        {
            if (text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }
}

public sealed record ScoredDocChunk(DocChunkDto Chunk, int Score);
