using VannaLight.Core.Abstractions;

namespace VannaLight.Api.Services.Docs;

public interface IDocChunkScorer
{
    int Score(DocChunkDto chunk, string question, IReadOnlyCollection<string> keywords, IReadOnlyCollection<string> partNumbers, DocTypeSchema schema);
    double NormalizeConfidence(int score);
}
