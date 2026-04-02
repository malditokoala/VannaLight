using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Core.UseCases;

public sealed class TrainExampleUseCase
{
    private readonly ITrainingStore _training;
    private readonly IPatternMatcherService _patternMatcher;

    public TrainExampleUseCase(ITrainingStore training, IPatternMatcherService patternMatcher)
    {
        _training = training;
        _patternMatcher = patternMatcher;
    }

    public async Task TrainAsync(
        string? question,
        string? sqlText,
        AskExecutionContext? executionContext,
        bool isVerified,
        CancellationToken ct)
    {
        var cleanQuestion = CleanText(question);
        var cleanSql = CleanText(sqlText);
        var cleanTenantKey = CleanText(executionContext?.TenantKey);
        var cleanDomain = CleanText(executionContext?.Domain);
        var cleanConnectionName = CleanText(executionContext?.ConnectionName);

        if (string.IsNullOrWhiteSpace(cleanQuestion) || string.IsNullOrWhiteSpace(cleanSql))
            throw new ArgumentException("La pregunta o el SQL están vacíos o corruptos.");

        var intentName = await _patternMatcher.InferIntentNameAsync(cleanQuestion, cleanDomain, ct);

        await _training.UpsertAsync(
            new TrainingExampleUpsert(
                cleanQuestion,
                cleanSql,
                string.IsNullOrWhiteSpace(cleanTenantKey) ? null : cleanTenantKey,
                string.IsNullOrWhiteSpace(cleanDomain) ? null : cleanDomain,
                string.IsNullOrWhiteSpace(cleanConnectionName) ? null : cleanConnectionName,
                string.IsNullOrWhiteSpace(intentName) ? null : intentName,
                isVerified,
                isVerified ? 100 : 0),
            ct);
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
