using System.Collections.Generic;

namespace VannaLight.Core.Models;

public record DocCitation(string DocId, string FileName, int PageNumber);

public record DocsAnswerResult(
    bool Success,
    string? AnswerText, // Nota: En tu DocsAnswerService lo llamas AnswerText, asegúrate de que coincida
    IReadOnlyList<DocCitation> Citations,
    string? ErrorMessage = null // Igual aquí, verifica si tu servicio espera ErrorMessage o Error
);

// Puedes dejar WiReindexResult aquí también si quieres centralizarlo
public record WiReindexResult(
    int TotalFiles,
    int Indexed,
    int Skipped,
    int Errors
);