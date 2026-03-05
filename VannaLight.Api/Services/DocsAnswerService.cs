using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.Sqlite;
using VannaLight.Api.Contracts;

namespace VannaLight.Api.Services;

public sealed class DocsAnswerService
{
    private readonly IConfiguration _config;
    private readonly DocTypeSchema _wiSchema;

    public DocsAnswerService(IConfiguration config)
    {
        _config = config;

        // Carga del esquema JSON. 
        // (En una arquitectura más avanzada, esto se inyectaría mediante un ISchemaLoader)
        var schemaPath = _config["Paths:SchemasPath"] ?? @"C:\VannaLight\Schemas";
        var file = Path.Combine(schemaPath, "work-instructions.json");

        if (File.Exists(file))
        {
            var json = File.ReadAllText(file);
            _wiSchema = JsonSerializer.Deserialize<DocTypeSchema>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        }
        else
        {
            // Fallback de seguridad por si el archivo no existe aún
            _wiSchema = new DocTypeSchema("default", "Documento", new ScoringConfig(new List<string>()), new List<FieldDef>());
        }
    }

    public async Task<DocsAnswerResult> AnswerAsync(string question, CancellationToken ct)
    {
        var q = (question ?? string.Empty).Trim();
        if (q.Length == 0)
            return new DocsAnswerResult(false, null, Array.Empty<DocCitation>(), "Pregunta vacía.");

        var sqlitePath = _config["Paths:Sqlite"] ?? "vanna_memory.db";
        var topK = int.TryParse(_config["Docs:TopKPages"], out var k) ? k : 6;
        var maxCites = int.TryParse(_config["Docs:MaxAnswerCitations"], out var mc) ? mc : 4;

        await using var conn = new SqliteConnection($"Data Source={sqlitePath}");
        await conn.OpenAsync(ct);

        // 1. Análisis de la pregunta usando el esquema dinámico
        var partNumbers = ExtractPartNumbers(q);
        var keywords = BuildDynamicKeywords(q, _wiSchema);

        // 2. Recuperación de fragmentos (Chunks)
        var chunks = (await conn.QueryAsync<DocChunkRow>(@"
SELECT d.DocId, d.FileName, c.PageNumber, c.Text
FROM DocChunks c
JOIN DocDocuments d ON d.DocId = c.DocId
WHERE d.Domain = 'work-instructions'
ORDER BY d.UpdatedUtc DESC
LIMIT 2000;")).ToList();

        if (chunks.Count == 0)
            return new DocsAnswerResult(false, null, Array.Empty<DocCitation>(), "No hay WI indexadas (DocChunks vacío).");

        // Barandal NP: si pidieron NP y no existe en ninguna WI => no inventar.
        if (partNumbers.Count > 0 && !chunks.Any(c => ContainsAny(c.Text, partNumbers)))
        {
            return new DocsAnswerResult(
                false,
                null,
                Array.Empty<DocCitation>(),
                $"No encontré evidencia para el número de parte ({string.Join(", ", partNumbers)}) en las WI indexadas. " +
                $"Verifica si el documento correcto está cargado en la carpeta DROP y reindexa."
            );
        }

        // 3. Rankeo (Scoring)
        var scored = chunks
            .Select(c =>
            {
                var score = ScoreChunk(c.Text, q, keywords, (int)c.PageNumber);
                if (partNumbers.Count > 0 && ContainsAny(c.Text, partNumbers))
                    score += 25; // Super boost si contiene el NP exacto

                return new ScoredChunk(c, score);
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .ToList();

        if (scored.Count == 0)
            return new DocsAnswerResult(false, null, Array.Empty<DocCitation>(), "No encontré evidencia en las WI indexadas.");

        // 4. Extracción y Construcción de Respuesta
        if (partNumbers.Count > 0)
        {
            // Con NP => Forzamos ganador y Página 1
            var winner = ResolveWinnerDoc(scored, chunks, partNumbers);
            var page1Text = await GetPageTextAsync(conn, winner.DocId, pageNumber: 1, ct);

            // Fallback: páginas topK del doc ganador con NP
            var fallbackTexts = scored
                .Where(s => s.Chunk.DocId == winner.DocId && ContainsAny(s.Chunk.Text, partNumbers))
                .Select(s => s.Chunk.Text)
                .ToList();

            var textsForExtraction = !string.IsNullOrWhiteSpace(page1Text)
                ? page1Text // Priorizamos la página 1 unificada
                : string.Join("\n", fallbackTexts);

            // LLAMADA AL MOTOR GENERALISTA
            var facts = ExtractionEngine.ExtractAll(_wiSchema, textsForExtraction);
            var answer = BuildDynamicAnswer(facts, _wiSchema, q);

            var citations = BuildWinnerCitations(
                winner.DocId,
                winner.FileName,
                hasPage1: !string.IsNullOrWhiteSpace(page1Text),
                fallback: scored.Where(s => s.Chunk.DocId == winner.DocId).ToList(),
                maxCites: maxCites);

            if (facts.Count == 0)
                answer = "Encontré páginas relevantes, pero no pude extraer el dato de forma confiable. Revisa las citas (página exacta).";

            return new DocsAnswerResult(true, answer, citations);
        }
        else
        {
            // Sin NP => Extraemos sobre el TopK concatenado
            var combinedText = string.Join("\n", scored.Select(x => x.Chunk.Text));

            // LLAMADA AL MOTOR GENERALISTA
            var facts = ExtractionEngine.ExtractAll(_wiSchema, combinedText);
            var answer = BuildDynamicAnswer(facts, _wiSchema, q);

            var citations = scored
                .Select(x => new DocCitation(x.Chunk.DocId, x.Chunk.FileName, (int)x.Chunk.PageNumber))
                .Distinct()
                .Take(maxCites)
                .ToList();

            if (facts.Count == 0)
                answer = "Encontré páginas relevantes, pero no pude extraer el dato de forma confiable. Revisa las citas (página exacta).";

            return new DocsAnswerResult(true, answer, citations);
        }
    }

    // =========================
    // Dynamic Helpers (Basados en Esquema)
    // =========================

    private string BuildDynamicAnswer(Dictionary<string, string> facts, DocTypeSchema schema, string questionText)
    {
        if (facts.Count == 0)
            return "Encontré páginas relevantes, pero no pude extraer el dato de forma confiable.";

        var lines = new List<string> { $"Ficha (según {schema.DisplayName}):" };
        var qLower = questionText.ToLowerInvariant();

        // Filtrar intención: verificamos si la pregunta menciona algún tag de los campos definidos
        var askedFields = schema.Fields
            .Where(f => f.Tags != null && f.Tags.Any(tag => qLower.Contains(tag)))
            .ToList();

        // Si el usuario no especificó, mostramos todo
        var fieldsToRender = askedFields.Count > 0 ? askedFields : schema.Fields;

        // Renderizamos dinámicamente respetando el "Order" del JSON
        foreach (var field in fieldsToRender.OrderBy(f => f.Order))
        {
            if (facts.TryGetValue(field.Key, out var extractedValue))
            {
                lines.Add($"- {field.DisplayLabel}: {extractedValue}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private List<string> BuildDynamicKeywords(string question, DocTypeSchema schema)
    {
        var qLower = (question ?? "").ToLowerInvariant();
        var list = new List<string>();

        // Agregamos como keywords de búsqueda todos los tags del esquema que el usuario haya mencionado
        foreach (var field in schema.Fields)
        {
            if (field.Tags != null && field.Tags.Any(t => qLower.Contains(t)))
            {
                list.AddRange(field.Tags);
            }
        }

        // Agregamos los NP explícitos
        list.AddRange(ExtractPartNumbers(question));

        return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private int ScoreChunk(string text, string q, List<string> keywords, int pageNumber)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        int score = 0;

        // Boost por keywords dinámicas
        foreach (var kw in keywords)
            if (text.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) score += 3;

        // Boost por tokens de la pregunta (largo > 3)
        foreach (var token in q.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Length < 4) continue;
            if (text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0) score += 1;
        }

        // Boost por página 1 (Header/Portada)
        if (pageNumber == 1) score += 8;

        // Boost por etiquetas importantes definidas en el JSON
        if (_wiSchema.Scoring?.BoostLabels != null)
        {
            foreach (var label in _wiSchema.Scoring.BoostLabels)
                if (text.IndexOf(label, StringComparison.OrdinalIgnoreCase) >= 0)
                    score += 3;
        }

        return score;
    }

    // =========================
    // Static Routing & DB Helpers
    // =========================

    private static List<string> ExtractPartNumbers(string q)
    {
        var baseParts = Regex.Matches(q, @"\d{4,}-\d{4,}")
            .Cast<Match>()
            .Select(m => m.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var parenParts = Regex.Matches(q, @"(?<base>\d{4,}-\d{4,})\((?<suffix>\d{1,4})\)")
            .Cast<Match>()
            .Select(m => new { Base = m.Groups["base"].Value, Suffix = m.Groups["suffix"].Value })
            .ToList();

        foreach (var x in parenParts)
        {
            if (x.Suffix.Length == 1 && x.Base.Length >= 1)
            {
                var expanded = x.Base.Substring(0, x.Base.Length - 1) + x.Suffix;
                baseParts.Add(expanded);
            }
            else
            {
                baseParts.Add($"{x.Base}({x.Suffix})");
            }
        }

        return baseParts.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool ContainsAny(string text, List<string> needles)
    {
        if (string.IsNullOrEmpty(text)) return false;
        foreach (var n in needles)
            if (text.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    private static WinnerDoc ResolveWinnerDoc(List<ScoredChunk> scoredTopK, List<DocChunkRow> allChunks, List<string> partNumbers)
    {
        var pnOnly = scoredTopK.Where(x => ContainsAny(x.Chunk.Text, partNumbers)).ToList();

        string winnerDocId;
        if (pnOnly.Count == 0)
        {
            winnerDocId = allChunks
                .Where(c => ContainsAny(c.Text, partNumbers))
                .GroupBy(c => c.DocId)
                .Select(g => new { DocId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .First().DocId;
        }
        else
        {
            winnerDocId = pnOnly
                .GroupBy(x => x.Chunk.DocId)
                .Select(g => new { DocId = g.Key, BestScore = g.Max(x => x.Score) })
                .OrderByDescending(x => x.BestScore)
                .First().DocId;
        }

        var fileName = allChunks.FirstOrDefault(c => c.DocId == winnerDocId)?.FileName ?? "(unknown)";
        return new WinnerDoc(winnerDocId, fileName);
    }

    private static async Task<string> GetPageTextAsync(SqliteConnection conn, string docId, int pageNumber, CancellationToken ct)
    {
        var parts = (await conn.QueryAsync<string>(@"
SELECT Text
FROM DocChunks
WHERE DocId = @DocId AND PageNumber = @PageNumber
ORDER BY rowid ASC;", new { DocId = docId, PageNumber = pageNumber })).ToList();

        return string.Join("\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static List<DocCitation> BuildWinnerCitations(string docId, string fileName, bool hasPage1, List<ScoredChunk> fallback, int maxCites)
    {
        if (hasPage1)
            return new List<DocCitation> { new DocCitation(docId, fileName, 1) };

        return fallback
            .Select(x => new DocCitation(x.Chunk.DocId, x.Chunk.FileName, (int)x.Chunk.PageNumber))
            .Distinct()
            .Take(maxCites)
            .ToList();
    }

    // =========================
    // Small models (Local to Service)
    // =========================
    private sealed record DocChunkRow(string DocId, string FileName, long PageNumber, string Text);
    private sealed record ScoredChunk(DocChunkRow Chunk, int Score);
    private sealed record WinnerDoc(string DocId, string FileName);
}