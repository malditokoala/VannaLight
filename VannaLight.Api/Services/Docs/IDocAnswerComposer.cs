using VannaLight.Api.Contracts;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Api.Services.Docs;

public interface IDocAnswerComposer
{
    Task<DocsAnswerResult> ComposeAsync(
        string sqlitePath,
        DocTypeSchema schema,
        DocsIntent intent,
        IReadOnlyList<ScoredDocChunk> scoredChunks,
        IReadOnlyList<DocChunkDto> allChunks,
        IReadOnlyCollection<string> partNumbers,
        int maxCitations,
        double confidence,
        CancellationToken ct);
}
