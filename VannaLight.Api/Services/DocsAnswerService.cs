using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using VannaLight.Api.Contracts;
using VannaLight.Api.Services.Docs;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;
using VannaLight.Core.Settings;

namespace VannaLight.Api.Services;

public sealed class DocsAnswerService : IDocsAnswerService
{
    private readonly IConfiguration _config;
    private readonly DocTypeSchema _schema;
    private readonly ILogger<DocsAnswerService> _log;
    private readonly IServiceProvider _services;
    private readonly IDocChunkRepository _repository;
    private readonly IDocChunkScorer _scorer;
    private readonly IDocAnswerComposer _answerComposer;
    private readonly string _sqlitePath;
    private readonly string _docsDomain;
    private readonly int _topK;
    private readonly int _maxCites;
    private readonly int _intentTimeoutSeconds;
    private readonly bool _debugLogs;

    public DocsAnswerService(
        IConfiguration config,
        IServiceProvider services,
        SqliteOptions sqliteOptions,
        ILogger<DocsAnswerService> log,
        IDocChunkRepository repository,
        IDocChunkScorer scorer,
        IDocAnswerComposer answerComposer)
    {
        _config = config;
        _services = services;
        _log = log;
        _repository = repository;
        _scorer = scorer;
        _answerComposer = answerComposer;

        _sqlitePath = sqliteOptions.DbPath;
        Directory.CreateDirectory(Path.GetDirectoryName(_sqlitePath)!);

        _docsDomain = _config["Docs:DefaultDomain"] ?? "work-instructions";
        _topK = int.TryParse(_config["Docs:TopKChunks"] ?? _config["Docs:TopKPages"], out var k) ? k : 6;
        _maxCites = int.TryParse(_config["Docs:MaxAnswerCitations"], out var mc) ? mc : 4;
        _intentTimeoutSeconds = int.TryParse(_config["Docs:IntentTimeoutSeconds"], out var timeoutSeconds)
            ? Math.Clamp(timeoutSeconds, 5, 120)
            : 25;
        _debugLogs = config.GetValue<bool>("Docs:DebugLogs");

        var schemaRel = _config["Paths:SchemasPath"] ?? "Schemas";
        var schemaDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, schemaRel));
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

    public async Task<DocsAnswerResult> AnswerAsync(string question, string? domain, CancellationToken ct)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var q = (question ?? string.Empty).Trim();
        if (q.Length == 0)
            return new DocsAnswerResult(false, null, Array.Empty<DocCitation>(), "Pregunta vacia.");

        var effectiveDomain = string.IsNullOrWhiteSpace(domain) ? _docsDomain : domain.Trim();
        var partNumbers = ExtractPartNumbers(q);
        var keywords = BuildDynamicKeywords(q, _schema);
        var retrieveStopwatch = Stopwatch.StartNew();
        var chunks = (await _repository.GetRecentChunksByDomainAsync(_sqlitePath, effectiveDomain, 3000, ct)).ToList();
        retrieveStopwatch.Stop();

        if (chunks.Count == 0)
            return new DocsAnswerResult(false, null, Array.Empty<DocCitation>(), $"No hay documentos indexados para el dominio '{effectiveDomain}'.");

        if (partNumbers.Count > 0 && !chunks.Any(c => ContainsAny(c.Text, partNumbers) || ContainsAny(c.PartNumbers, partNumbers)))
        {
            return new DocsAnswerResult(
                false,
                null,
                Array.Empty<DocCitation>(),
                $"No encontre evidencia para el numero de parte ({string.Join(", ", partNumbers)}) en los documentos indexados.");
        }

        var scoreStopwatch = Stopwatch.StartNew();
        var scored = chunks
            .Select(chunk => new ScoredDocChunk(chunk, _scorer.Score(chunk, q, keywords, partNumbers, _schema)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Chunk.PageNumber)
            .ThenBy(x => x.Chunk.ChunkOrder)
            .Take(_topK)
            .ToList();
        scoreStopwatch.Stop();

        if (scored.Count == 0)
            return new DocsAnswerResult(false, null, Array.Empty<DocCitation>(), "No encontre evidencia relevante en los documentos indexados.");

        if (_debugLogs)
        {
            var best = scored[0];
            _log.LogInformation(
                "[Docs] Domain={Domain} Best={File} p{Page}#{Chunk} Score={Score}",
                effectiveDomain,
                best.Chunk.FileName,
                best.Chunk.PageNumber,
                best.Chunk.ChunkOrder,
                best.Score);
        }

        var confidence = _scorer.NormalizeConfidence(scored[0].Score);
        IDocsIntentRouter router;
        try
        {
            router = _services.GetRequiredService<IDocsIntentRouter>();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[Docs] No se pudo inicializar el router documental para Domain={Domain}", effectiveDomain);
            return BuildRouterInitializationError();
        }

        DocsIntent intent;
        var parseStopwatch = Stopwatch.StartNew();
        using (var parseTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            parseTimeoutCts.CancelAfter(TimeSpan.FromSeconds(_intentTimeoutSeconds));
            try
            {
                intent = await router.ParseAsync(q, _schema, parseTimeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                if (ct.IsCancellationRequested)
                    throw;

                _log.LogWarning(
                    "[Docs] Timeout parseando intención documental para Domain={Domain} después de {TimeoutSeconds}s",
                    effectiveDomain,
                    _intentTimeoutSeconds);
                return BuildRouterTimeoutError(_intentTimeoutSeconds);
            }
        catch (TimeoutException ex)
        {
                _log.LogWarning(
                    ex,
                    "[Docs] TimeoutException parseando intención documental para Domain={Domain} después de {TimeoutSeconds}s",
                    effectiveDomain,
                    _intentTimeoutSeconds);
                return BuildRouterTimeoutError(_intentTimeoutSeconds);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "[Docs] Fallo el parseo de intención documental para Domain={Domain}", effectiveDomain);
                return BuildRouterInitializationError();
            }
        }
        parseStopwatch.Stop();

        var composeTimeoutSeconds = Math.Max(_intentTimeoutSeconds, 15);
        using var composeTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        composeTimeoutCts.CancelAfter(TimeSpan.FromSeconds(composeTimeoutSeconds));

        try
        {
            var composeStopwatch = Stopwatch.StartNew();
            var result = await _answerComposer.ComposeAsync(
                _sqlitePath,
                _schema,
                intent,
                scored,
                chunks,
                partNumbers,
                _maxCites,
                confidence,
                composeTimeoutCts.Token);
            composeStopwatch.Stop();
            totalStopwatch.Stop();

            if (_debugLogs)
            {
                _log.LogInformation(
                    "[DocsPerf] Domain={Domain} Chunks={ChunkCount} Top={TopCount} RetrieveMs={RetrieveMs} ScoreMs={ScoreMs} ParseMs={ParseMs} ComposeMs={ComposeMs} TotalMs={TotalMs}",
                    effectiveDomain,
                    chunks.Count,
                    scored.Count,
                    retrieveStopwatch.ElapsedMilliseconds,
                    scoreStopwatch.ElapsedMilliseconds,
                    parseStopwatch.ElapsedMilliseconds,
                    composeStopwatch.ElapsedMilliseconds,
                    totalStopwatch.ElapsedMilliseconds);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            if (ct.IsCancellationRequested)
                throw;

            _log.LogWarning(
                "[Docs] Timeout componiendo respuesta documental para Domain={Domain} después de {TimeoutSeconds}s",
                effectiveDomain,
                composeTimeoutSeconds);
            return BuildComposeTimeoutError(composeTimeoutSeconds);
        }
        catch (TimeoutException ex)
        {
            _log.LogWarning(ex, "[Docs] TimeoutException componiendo respuesta documental para Domain={Domain}", effectiveDomain);
            return BuildComposeTimeoutError(composeTimeoutSeconds);
        }
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
                baseParts.Add(x.Base[..^1] + x.Suffix);
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

    private static DocsAnswerResult BuildRouterInitializationError()
    {
        return new DocsAnswerResult(
            false,
            null,
            Array.Empty<DocCitation>(),
            "No se pudo inicializar el modelo documental local. Revisa la configuración del modelo o el backend de aceleración local.");
    }

    private static DocsAnswerResult BuildRouterTimeoutError(int timeoutSeconds)
    {
        return new DocsAnswerResult(
            false,
            null,
            Array.Empty<DocCitation>(),
            $"El análisis documental tardó más de {timeoutSeconds} segundos y se canceló. Intenta refinar la pregunta o vuelve a intentarlo.");
    }

    private static DocsAnswerResult BuildComposeTimeoutError(int timeoutSeconds)
    {
        return new DocsAnswerResult(
            false,
            null,
            Array.Empty<DocCitation>(),
            $"La respuesta documental tardó más de {timeoutSeconds} segundos en completarse y se canceló. Intenta con una pregunta más específica.");
    }
}
