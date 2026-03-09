using System.Text.Json;
using System.Text.RegularExpressions;
// VannaLight.Api.Contracts;
using VannaLight.Api.Services.Docs;
using VannaLight.Core.Abstractions; // Interfaz y DTOs del Core
using VannaLight.Core.Models;

namespace VannaLight.Api.Services;

// TODO (Deuda Técnica - Fase de Testing): 
// 1. Extraer lógicas de Dominio (Extracción/Scoring) a servicios de Core.
// 2. Mover modelos (DocTypeSchema, DocsIntent) a VannaLight.Core.Models.
// 3. Romper este God Object para facilitar testing unitario.
public sealed class DocsAnswerService : IDocsAnswerService
{
    private readonly IConfiguration _config;
    private readonly DocTypeSchema _wiSchema;
    private readonly ILogger<DocsAnswerService> _log;
    private readonly IHostEnvironment _env;
    private readonly IDocsIntentRouter _router;
    private readonly IDocChunkRepository _repository; // <-- NUEVA DEPENDENCIA (Clean Architecture)

    private readonly string _sqlitePath;
    private readonly int _topK;
    private readonly int _maxCites;
    private readonly bool _debugLogs;

    public DocsAnswerService(
        IConfiguration config,
        IHostEnvironment env,
        ILogger<DocsAnswerService> log,
        IDocsIntentRouter router,
        IDocChunkRepository repository)
    {
        _config = config;
        _log = log;
        _env = env;
        _router = router;
        _repository = repository;

        // ---- Settings cache (no recalcular por request) ----
        var sqliteRel = _config["Paths:Sqlite"] ?? "Data/vanna_memory.db";
        _sqlitePath = Path.GetFullPath(Path.Combine(_env.ContentRootPath, sqliteRel));
        Directory.CreateDirectory(Path.GetDirectoryName(_sqlitePath)!);

        _topK = int.TryParse(_config["Docs:TopKPages"], out var k) ? k : 6;
        _maxCites = int.TryParse(_config["Docs:MaxAnswerCitations"], out var mc) ? mc : 4;

        _debugLogs = _env.IsDevelopment() || _config.GetValue<bool>("Docs:DebugLogs");

        // ---- Schema load ----
        var schemaRel = _config["Paths:SchemasPath"] ?? "Schemas";
        var schemaDir = Path.GetFullPath(Path.Combine(_env.ContentRootPath, schemaRel));
        var file = Path.Combine(schemaDir, "work-instructions.json");

        if (File.Exists(file))
        {
            var json = File.ReadAllText(file);
            _wiSchema = JsonSerializer.Deserialize<DocTypeSchema>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            )!;
        }
        else
        {
            _wiSchema = new DocTypeSchema("default", "Documento", new ScoringConfig(new()), new());
        }

        _log.LogInformation("[Docs] Schema file={File} Exists={Exists} Fields={Fields}",
            file, File.Exists(file), _wiSchema.Fields.Count);

        _log.LogInformation("[Docs] SqlitePath={SqlitePath} TopK={TopK} MaxCites={MaxCites} DebugLogs={Debug}",
            _sqlitePath, _topK, _maxCites, _debugLogs);
    }

    public async Task<DocsAnswerResult> AnswerAsync(string question, CancellationToken ct)
    {
        var q = (question ?? string.Empty).Trim();
        if (q.Length == 0)
            return new DocsAnswerResult(false, null, Array.Empty<DocCitation>(), "Pregunta vacía.");

        var partNumbers = ExtractPartNumbers(q);
        var keywords = BuildDynamicKeywords(q, _wiSchema);

        if (_debugLogs)
        {
            _log.LogInformation("[Docs] Q={Q}", q);
            _log.LogInformation("[Docs] PartNumbers={PN} KeywordsCount={KWCount}",
                string.Join(", ", partNumbers), keywords.Count);
        }

        // =========================================================================
        // DEUDA SALDADA: LLAMADA LIMPIA AL REPOSITORIO (SIN SQL NI CONEXIONES AQUÍ)
        // =========================================================================
        var chunks = (await _repository.GetRecentChunksByDomainAsync(_sqlitePath, "work-instructions", 2000, ct)).ToList();

        if (chunks.Count == 0)
            return new DocsAnswerResult(false, null, Array.Empty<DocCitation>(), "No hay WI indexadas (DocChunks vacío).");

        // Barandal NP
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

        var scored = chunks
            .Select(c =>
            {
                var score = ScoreChunk(c.Text, q, keywords, (int)c.PageNumber);
                if (partNumbers.Count > 0 && ContainsAny(c.Text, partNumbers))
                    score += 25;
                return new ScoredChunk(c, score); // Usa el DocChunkDto del Core
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(_topK)
            .ToList();

        if (scored.Count == 0)
            return new DocsAnswerResult(false, null, Array.Empty<DocCitation>(), "No encontré evidencia en las WI indexadas.");

        _log.LogInformation("[Docs] ScoredTopK={Count} Best={Best}",
            scored.Count,
            $"{scored[0].Chunk.FileName} p{scored[0].Chunk.PageNumber} score={scored[0].Score}");

        // ---------- Con NP (winner + page1 hard) ----------
        if (partNumbers.Count > 0)
        {
            var winner = ResolveWinnerDoc(scored, chunks, partNumbers);

            // =========================================================================
            // DEUDA SALDADA: LLAMADA LIMPIA AL REPOSITORIO PARA LA PÁGINA 1
            // =========================================================================
            var page1Parts = await _repository.GetPageTextPartsAsync(_sqlitePath, winner.DocId, 1, ct);
            var page1Text = string.Join("\n", page1Parts.Where(p => !string.IsNullOrWhiteSpace(p)));

            var fallbackTexts = scored
                .Where(s => s.Chunk.DocId == winner.DocId && ContainsAny(s.Chunk.Text, partNumbers))
                .Select(s => s.Chunk.Text)
                .ToList();

            var textsForExtraction = !string.IsNullOrWhiteSpace(page1Text)
                ? page1Text
                : string.Join("\n", fallbackTexts);

            if (_debugLogs)
            {
                _log.LogInformation("[Docs] Winner DocId={DocId} File={File} Page1Len={P1Len} ExtractTextLen={Len}",
                    winner.DocId, winner.FileName, page1Text?.Length ?? 0, textsForExtraction?.Length ?? 0);
            }

            var facts = ExtractionEngine.ExtractAll(_wiSchema, textsForExtraction);

            if (_debugLogs)
            {
                _log.LogInformation("[Docs] FactsCount={Count} Facts={Facts}",
                    facts.Count,
                    facts.Count == 0 ? "(none)" : string.Join(" | ", facts.Select(kv => $"{kv.Key}={kv.Value}")));
            }

            var citations = BuildWinnerCitations(
                winner.DocId,
                winner.FileName,
                hasPage1: !string.IsNullOrWhiteSpace(page1Text),
                fallback: scored.Where(s => s.Chunk.DocId == winner.DocId).ToList(),
                maxCites: _maxCites);

            if (facts.Count == 0)
                return new DocsAnswerResult(true,
                    "Encontré páginas relevantes, pero no pude extraer el dato de forma confiable. Revisa las citas (página exacta).",
                    citations);

            // <-- RUTEO SEMÁNTICO (LLM) -->
            var intent = await _router.ParseAsync(q, _wiSchema, ct);

            // <-- RENDERIZADO DUAL -->
            var answer = WiAnswerBuilder.Build(facts, intent, _wiSchema);
            return new DocsAnswerResult(true, answer, citations);
        }

        // ---------- Sin NP (topK concatenado) ----------
        var combinedText = string.Join("\n", scored.Select(x => x.Chunk.Text));
        var factsNoPn = ExtractionEngine.ExtractAll(_wiSchema, combinedText);

        if (_debugLogs)
        {
            _log.LogInformation("[Docs] NoPN CombinedTextLen={Len} FactsCount={Count}",
                combinedText.Length, factsNoPn.Count);
        }

        var citationsNoPn = scored
            .Select(x => new DocCitation(x.Chunk.DocId, x.Chunk.FileName, (int)x.Chunk.PageNumber))
            .Distinct()
            .Take(_maxCites)
            .ToList();

        if (factsNoPn.Count == 0)
            return new DocsAnswerResult(true,
                "Encontré páginas relevantes, pero no pude extraer el dato de forma confiable. Revisa las citas (página exacta).",
                citationsNoPn);

        // <-- RUTEO SEMÁNTICO (LLM) -->
        var intentNoPn = await _router.ParseAsync(q, _wiSchema, ct);

        // <-- RENDERIZADO DUAL -->
        var answerNoPn = WiAnswerBuilder.Build(factsNoPn, intentNoPn, _wiSchema);
        return new DocsAnswerResult(true, answerNoPn, citationsNoPn);
    }

    // =========================
    // Helpers
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
                baseParts.Add(x.Base.Substring(0, x.Base.Length - 1) + x.Suffix);
            else
                baseParts.Add($"{x.Base}({x.Suffix})");
        }

        return baseParts.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private List<string> BuildDynamicKeywords(string question, DocTypeSchema schema)
    {
        var qLower = (question ?? "").ToLowerInvariant();
        var list = new List<string>();

        foreach (var field in schema.Fields)
            if (field.Tags != null && field.Tags.Any(t => qLower.Contains(t)))
                list.AddRange(field.Tags);

        list.AddRange(ExtractPartNumbers(question));
        return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private int ScoreChunk(string text, string q, List<string> keywords, int pageNumber)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var score = 0;

        foreach (var kw in keywords)
            if (text.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) score += 3;

        foreach (var token in q.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            if (token.Length >= 4 && text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0) score += 1;

        if (pageNumber == 1) score += 8;

        if (_wiSchema.Scoring?.BoostLabels != null)
            foreach (var label in _wiSchema.Scoring.BoostLabels)
                if (text.IndexOf(label, StringComparison.OrdinalIgnoreCase) >= 0) score += 3;

        return score;
    }

    private static bool ContainsAny(string text, List<string> needles)
    {
        if (string.IsNullOrEmpty(text)) return false;
        foreach (var n in needles)
            if (text.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    private static WinnerDoc ResolveWinnerDoc(List<ScoredChunk> scoredTopK, List<DocChunkDto> allChunks, List<string> partNumbers)
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
    // Local models
    // =========================
    // Nota: DocChunkRow se eliminó en favor de DocChunkDto del Core.
    private sealed record ScoredChunk(DocChunkDto Chunk, int Score);
    private sealed record WinnerDoc(string DocId, string FileName);
}