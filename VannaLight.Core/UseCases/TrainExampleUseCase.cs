using VannaLight.Core.Abstractions;

namespace VannaLight.Core.UseCases;

public sealed class TrainExampleUseCase
{
    private readonly ITrainingStore _training;

    public TrainExampleUseCase(ITrainingStore training)
        => _training = training;

    public async Task TrainAsync(string? question, string? sqlText, CancellationToken ct)
    {
        var cleanQuestion = CleanText(question);
        var cleanSql = CleanText(sqlText);

        if (string.IsNullOrWhiteSpace(cleanQuestion) || string.IsNullOrWhiteSpace(cleanSql))
            throw new ArgumentException("La pregunta o el SQL están vacíos o corruptos.");

        // Política: Upsert (no duplicar por Question)
        await _training.UpsertByQuestionAsync(cleanQuestion, cleanSql, ct);
    }

    private static string CleanText(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;

        return s.Replace("\0", "")
                .Replace("\uFFFF", "")
                .Replace("홚", "")
                .Trim();
    }
}