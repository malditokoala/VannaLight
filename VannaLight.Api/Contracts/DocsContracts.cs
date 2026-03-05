namespace VannaLight.Api.Contracts;

public record DocCitation(string DocId, string FileName, int PageNumber);

public record DocsAnswerResult(
    bool Success,
    string? Answer,
    IReadOnlyList<DocCitation> Citations,
    string? Error = null
);

public record WiReindexResult(
    int TotalFiles,
    int Indexed,
    int Skipped,
    int Errors
);