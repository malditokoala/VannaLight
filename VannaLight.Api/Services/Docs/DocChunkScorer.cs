using VannaLight.Core.Abstractions;

namespace VannaLight.Api.Services.Docs;

public sealed class DocChunkScorer : IDocChunkScorer
{
    public int Score(DocChunkDto chunk, string question, IReadOnlyCollection<string> keywords, IReadOnlyCollection<string> partNumbers, DocTypeSchema schema)
    {
        if (string.IsNullOrWhiteSpace(chunk.Text))
            return 0;

        var score = 0;
        var tokens = question.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 4)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var keyword in keywords)
        {
            if (chunk.Text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) score += DocScoringWeights.KeywordTextMatch;
            if (!string.IsNullOrWhiteSpace(chunk.ChunkTitle) && chunk.ChunkTitle.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) score += DocScoringWeights.KeywordTitleMatch;
            if (!string.IsNullOrWhiteSpace(chunk.SectionName) && chunk.SectionName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) score += DocScoringWeights.KeywordSectionMatch;
        }

        foreach (var token in tokens)
        {
            if (chunk.Text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0) score += DocScoringWeights.QuestionTokenTextMatch;
            if (!string.IsNullOrWhiteSpace(chunk.NormalizedTokens) && chunk.NormalizedTokens.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0) score += DocScoringWeights.QuestionTokenNormalizedMatch;
        }

        if (schema.Scoring?.BoostLabels != null)
        {
            foreach (var label in schema.Scoring.BoostLabels)
            {
                if (chunk.Text.IndexOf(label, StringComparison.OrdinalIgnoreCase) >= 0)
                    score += DocScoringWeights.BoostLabelMatch;
            }
        }

        if (chunk.IsCoverPage) score += DocScoringWeights.CoverPage;
        if (chunk.ChunkOrder <= 2) score += DocScoringWeights.EarlyChunk;
        if (chunk.PageNumber == 1) score += DocScoringWeights.FirstPage;

        if (partNumbers.Count > 0)
        {
            if (ContainsAny(chunk.Text, partNumbers)) score += DocScoringWeights.PartNumberTextMatch;
            if (ContainsAny(chunk.PartNumbers, partNumbers)) score += DocScoringWeights.PartNumberMetadataMatch;
        }

        return score;
    }

    public double NormalizeConfidence(int score)
        => Math.Round(Math.Min(DocScoringWeights.MaxConfidence, Math.Max(DocScoringWeights.MinConfidence, score / DocScoringWeights.ConfidenceDivisor)), 2);

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
