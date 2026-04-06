using System.Text.Json;
using System.Text.RegularExpressions;
using VannaLight.Api.Services.Docs;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Api.Services;

public sealed class DocsAnswerService : IDocsAnswerService
{
    private readonly IConfiguration _config;
    private readonly DocTypeSchema _schema;
    private readonly ILogger<DocsAnswerService> _log;
    private readonly IHostEnvironment _env;
    private readonly IDocsIntentRouter _router;
    private readonly IDocChunkRepository _repository;
    private readonly IDocChunkScorer _scorer;
    private readonly IDocAnswerComposer _answerComposer;
    private readonly string _sqlitePath;
    private readonly string _docsDomain;
    private readonly int _topK;
    private readonly int _maxCites;
    private readonly bool _debugLogs;

    public DocsAnswerService(
        IConfiguration config,
        IHostEnvironment env,
        ILogger<DocsAnswerService> log,
        IDocsIntentRouter router,
        IDocChunkRepository repository,
        IDocChunkScorer scorer,
        IDocAnswerComposer answerComposer)
    {
        _config = config;
        _env = env;
        _log = log;
        _router = router;
        _repository = repository;
        _scorer = scorer;
        _answerComposer = answerComposer;

        var sqliteRel = _config["Paths:Sqlite"] ?? "Data/vanna_memory.db";
        _sqlitePath = Path.GetFullPath(Path.Combine(_env.ContentRootPath, sqliteRel));
        Directory.CreateDirectory(Path.GetDirectoryName(_sqlitePath)!);

        _docsDomain = _config["Docs:DefaultDomain"] ?? "work-instructions";
        _topK = int.TryParse(_config["Docs:TopKChunks"] ?? _config["Docs:TopKPages"], out var k) ? k : 6;
        _maxCites = int.TryParse(_config["Docs:MaxAnswerCitations"], out var mc) ? mc : 4;
        _debugLogs = _env.IsDevelopment() || _config.GetValue<bool>("Docs:DebugLogs");

        var schemaRel = _config["Paths:SchemasPath"] ?? "Schemas";
        var schemaDir = Path.GetFullPath(Path.Combine(_env.ContentRootPath, schemaRel));
        var schemaFile = _config["Docs:SchemaFile"] ?? "work-instructions.json";
        var file = Path.Combine(schemaDir, schemaFile);

        if (File.Exists(file))
        {
            var json = File.ReadAllText(file);
            _schema = JsonSerializer.Deserialize<DocTypeSchema>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new DocTypeSchema("default", "Documento", new ScoringConfig(new()), new());
        }
        else
        {
            _schema = new DocTypeSchema("default", "Documento", new ScoringConfig(new()), new());
        }
    }

    public async Task<DocsAnswerResult> AnswerAsync(string question, CancellationToken ct)
    {
        var q = (question ?? string.Empty).Trim();
        if (q.Length == 0)
            return new DocsAnswerResult(false, null, Array.Empty<DocCitation>(), "Pregunta vacia.");

        var partNumbers = ExtractPartNumbers(q);
        var keywords = BuildDynamicKeywords(q, _schema);
        var chunks = (await _repository.GetRecentChunksByDomainAsync(_sqlitePath, _docsDomain, 3000, ct)).ToList();

        if (chunks.Count == 0)
            return new DocsAnswerResult(false, null, Array.Empty<DocCitation>(), "No hay documentos indexados para este dominio.");

        if (partNumbers.Count > 0 && !chunks.Any(c => ContainsAny(c.Text, partNumbers) || ContainsAny(c.PartNumbers, partNumbers)))
        {
            return new DocsAnswerResult(
                false,
                null,
                Array.Empty<DocCitation>(),
                $"No encontre evidencia para el numero de parte ({string.Join(", ", partNumbers)}) en los documentos indexados.");
        }

        var scored = chunks
            .Select(chunk => new ScoredDocChunk(chunk, _scorer.Score(chunk, q, keywords, partNumbers, _schema)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Chunk.PageNumber)
            .ThenBy(x => x.Chunk.ChunkOrder)
            .Take(_topK)
            .ToList();

        if (scored.Count == 0)
            return new DocsAnswerResult(false, null, Array.Empty<DocCitation>(), "No encontre evidencia relevante en los documentos indexados.");

        if (_debugLogs)
        {
            var best = scored[0];
            _log.LogInformation("[Docs] Domain={Domain} Best={File} p{Page}#{Chunk} Score={Score}",
                _docsDomain, best.Chunk.FileName, best.Chunk.PageNumber, best.Chunk.ChunkOrder, best.Score);
        }

        var confidence = _scorer.NormalizeConfidence(scored[0].Score);
        var intent = await _router.ParseAsync(q, _schema, ct);

        return await _answerComposer.ComposeAsync(
            _sqlitePath,
            _schema,
            intent,
            scored,
            chunks,
            partNumbers,
            _maxCites,
            confidence,
            ct);
    }

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

    private static bool ContainsAny(string? text, List<string> needles)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        foreach (var n in needles)
        {
            if (text.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private List<string> BuildDynamicKeywords(string question, DocTypeSchema schema)
    {
        var qLower = (question ?? string.Empty).ToLowerInvariant();
        var list = new List<string>();

        foreach (var field in schema.Fields)
        {
            if (field.Tags != null && field.Tags.Any(t => qLower.Contains(t, StringComparison.OrdinalIgnoreCase)))
                list.AddRange(field.Tags);

            if (!string.IsNullOrWhiteSpace(field.DisplayLabel) && qLower.Contains(field.DisplayLabel, StringComparison.OrdinalIgnoreCase))
                list.Add(field.DisplayLabel);
        }

        list.AddRange(ExtractPartNumbers(question));
        return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

}
