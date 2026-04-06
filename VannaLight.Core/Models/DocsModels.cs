using System.Collections.Generic;

namespace VannaLight.Core.Models;

public record DocCitation(
    string DocId,
    string FileName,
    int PageNumber,
    string? Snippet = null,
    string? Section = null,
    double? Confidence = null);

public record DocsAnswerResult(
    bool Success,
    string? AnswerText,
    IReadOnlyList<DocCitation> Citations,
    string? ErrorMessage = null,
    double? ConfidenceScore = null
);

public record DocumentReindexResult(
    int TotalFiles,
    int Indexed,
    int Skipped,
    int Errors
);

// Compatibilidad temporal con el naming legacy.
public record WiReindexResult(
    int TotalFiles,
    int Indexed,
    int Skipped,
    int Errors
) : DocumentReindexResult(TotalFiles, Indexed, Skipped, Errors);
