namespace VannaLight.Api.Services.Docs;

internal static class DocScoringWeights
{
    public const int KeywordTextMatch = 4;
    public const int KeywordTitleMatch = 6;
    public const int KeywordSectionMatch = 5;
    public const int QuestionTokenTextMatch = 1;
    public const int QuestionTokenNormalizedMatch = 2;
    public const int BoostLabelMatch = 3;
    public const int CoverPage = 4;
    public const int EarlyChunk = 2;
    public const int FirstPage = 2;
    public const int PartNumberTextMatch = 30;
    public const int PartNumberMetadataMatch = 35;
    public const double MinConfidence = 0.15d;
    public const double MaxConfidence = 0.99d;
    public const double ConfidenceDivisor = 45d;
}
