using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;
using VannaLight.Core.Settings;

namespace VannaLight.Infrastructure.Retrieval;

public class LocalRetriever : IRetriever
{
    private readonly ISchemaStore _schemaStore;
    private readonly ITrainingStore _trainingStore;
    private readonly AppSettings _settings;
    private readonly ISystemConfigProvider _systemConfigProvider;
    private readonly IMemoryCache _cache;

    private sealed record DictRow(string Term, string MappedTokens);

    public LocalRetriever(
        ISchemaStore schemaStore,
        ITrainingStore trainingStore,
        AppSettings settings,
        ISystemConfigProvider systemConfigProvider,
        IMemoryCache cache)
    {
        _schemaStore = schemaStore;
        _trainingStore = trainingStore;
        _settings = settings;
        _systemConfigProvider = systemConfigProvider;
        _cache = cache;
    }

    public async Task<RetrievalContext> RetrieveAsync(
        string sqlitePath,
        string question,
        AskExecutionContext executionContext,
        string? intentName,
        CancellationToken ct)
    {
        var normalizedDomain = string.IsNullOrWhiteSpace(executionContext?.Domain)
            ? string.Empty
            : executionContext.Domain.Trim();
        var normalizedTenantKey = string.IsNullOrWhiteSpace(executionContext?.TenantKey)
            ? string.Empty
            : executionContext.TenantKey.Trim();
        var normalizedConnectionName = string.IsNullOrWhiteSpace(executionContext?.ConnectionName)
            ? string.Empty
            : executionContext.ConnectionName.Trim();
        var systemProfileKey = string.IsNullOrWhiteSpace(executionContext?.SystemProfileKey)
            ? null
            : executionContext.SystemProfileKey.Trim();

        var retrievalTopExamples = await _systemConfigProvider.GetIntAsync("Retrieval", "TopExamples", systemProfileKey, ct)
            ?? _settings.Retrieval.TopExamples;
        var retrievalMinExampleScore = await _systemConfigProvider.GetDoubleAsync("Retrieval", "MinExampleScore", systemProfileKey, ct)
            ?? _settings.Retrieval.MinExampleScore;
        var retrievalTopSchemaDocs = await _systemConfigProvider.GetIntAsync("Retrieval", "TopSchemaDocs", systemProfileKey, ct)
            ?? _settings.Retrieval.TopSchemaDocs;
        var retrievalFallbackSchemaDocs = await _systemConfigProvider.GetIntAsync("Retrieval", "FallbackSchemaDocs", systemProfileKey, ct)
            ?? _settings.Retrieval.FallbackSchemaDocs;
        var synonyms = await LoadSynonymsAsync(sqlitePath, normalizedDomain, systemProfileKey, ct);

        var strictQueryTokens = TokenizeStrict(question);
        var expandedQueryTokens = TokenizeWithSynonyms(strictQueryTokens, synonyms);

        var allExamples = await _trainingStore.GetAllTrainingExamplesAsync(sqlitePath, ct);
        var normalizedIntent = string.IsNullOrWhiteSpace(intentName) ? null : intentName.Trim();
        var rankedExamples = allExamples
            .Where(ex => ex.HasTrustedContext)
            .Where(ex => string.Equals(ex.TenantKey, normalizedTenantKey, StringComparison.OrdinalIgnoreCase))
            .Where(ex => string.Equals(ex.Domain ?? string.Empty, normalizedDomain, StringComparison.OrdinalIgnoreCase))
            .Where(ex => string.Equals(ex.ConnectionName, normalizedConnectionName, StringComparison.OrdinalIgnoreCase))
            .Select(ex => new RetrievedExample(ex, CalculateExampleScore(ex, strictQueryTokens, normalizedDomain, normalizedIntent)))
            .Where(r => r.Score >= retrievalMinExampleScore)
            .OrderByDescending(r => r.Score)
            .Take(retrievalTopExamples)
            .ToList();

        var allSchemaDocs = await _schemaStore.GetAllSchemaDocsAsync(sqlitePath, ct);
        var rankedSchemaDocs = allSchemaDocs
            .Select(doc => new RetrievedSchemaDoc(doc, CalculateScore(TokenizeStrict(doc.DocText), expandedQueryTokens)))
            .OrderByDescending(r => r.Score)
            .ToList();

        var topSchemaDocs = rankedSchemaDocs
            .Where(r => r.Score > 0)
            .Take(retrievalTopSchemaDocs)
            .ToList();

        // Fallback inteligente
        if (topSchemaDocs.Count == 0)
        {
            var fallbackN = retrievalFallbackSchemaDocs > 0
                ? retrievalFallbackSchemaDocs
                : retrievalTopSchemaDocs;

            topSchemaDocs = rankedSchemaDocs.Take(fallbackN).ToList();
        }

        return new RetrievalContext(rankedExamples, topSchemaDocs);
    }

    private async Task<Dictionary<string, string[]>> LoadSynonymsAsync(string sqlitePath, string domain, string? systemProfileKey, CancellationToken ct)
    {
        var configuredDomain = await _systemConfigProvider.GetValueAsync("Retrieval", "Domain", systemProfileKey, ct);
        var effectiveDomain = !string.IsNullOrWhiteSpace(domain)
            ? domain.Trim()
            : configuredDomain?.Trim() ?? _settings.Retrieval.Domain ?? "global";
        var cacheKey = $"BusinessSynonyms::{sqlitePath}::{effectiveDomain}";

        if (_cache.TryGetValue(cacheKey, out Dictionary<string, string[]>? cached) && cached != null)
        {
            return cached;
        }

        var dict = new Dictionary<string, string[]>();

        try
        {
            using var connection = new SqliteConnection($"Data Source={sqlitePath}");
            await connection.OpenAsync(ct);

            // Filtramos por IsEnabled y por el Domain configurado
            var sql = @"SELECT Term, MappedTokens 
                        FROM BusinessDictionary 
                        WHERE IsEnabled = 1 
                          AND (Domain IS NULL OR Domain = @Domain)";

            var rows = await connection.QueryAsync<DictRow>(
                new CommandDefinition(sql, new { Domain = effectiveDomain }, cancellationToken: ct));

            foreach (var row in rows)
            {
                string term = row.Term.ToLowerInvariant().Trim();
                dict[term] = row.MappedTokens
                                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(x => x.Trim().ToLowerInvariant())
                                .ToArray();
            }

            _cache.Set(cacheKey, dict, TimeSpan.FromMinutes(10));
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1 && ex.Message.Contains("no such table"))
        {
            // Ignoramos silenciosamente si la tabla aún no se crea
        }

        return dict;
    }

    private static double CalculateScore(HashSet<string> documentTokens, HashSet<string> queryTokens)
    {
        var score = 0;
        foreach (var t in queryTokens)
        {
            if (documentTokens.Contains(t)) score++;
        }
        return score;
    }

    private static double CalculateExampleScore(
        TrainingExample example,
        HashSet<string> queryTokens,
        string? domain,
        string? intentName)
    {
        var total = CalculateScore(TokenizeStrict(example.Question), queryTokens);

        if (example.IsVerified)
            total += 8;

        if (!string.IsNullOrWhiteSpace(domain) &&
            !string.IsNullOrWhiteSpace(example.Domain) &&
            string.Equals(example.Domain, domain, StringComparison.OrdinalIgnoreCase))
        {
            total += 6;
        }

        if (!string.IsNullOrWhiteSpace(intentName) &&
            !string.IsNullOrWhiteSpace(example.IntentName) &&
            string.Equals(example.IntentName, intentName, StringComparison.OrdinalIgnoreCase))
        {
            total += 10;
        }

        if (example.Priority > 0)
            total += Math.Min(example.Priority, 100) / 20.0;

        return total;
    }

    private static string RemoveDiacritics(string text)
    {
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder(capacity: normalizedString.Length);

        for (int i = 0; i < normalizedString.Length; i++)
        {
            char c = normalizedString[i];
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }

    // --- TU TOKENIZADOR ACTUALIZADO ---
    private static HashSet<string> TokenizeStrict(string text)
    {
        // 1. Quitamos signos de puntuación
        var charsToRemove = new[] { '.', ',', '?', '¿', '!', '¡', '(', ')', '[', ']', '-', '_', ':', ';', '\'', '"', '/', '\\' };
        var cleanText = new string(text.Where(c => !charsToRemove.Contains(c)).ToArray());

        // 2. Pasamos a minúsculas
        cleanText = cleanText.ToLowerInvariant();

        // 3. ¡LA MAGIA! Destruimos los acentos (ej. "producción" -> "produccion")
        cleanText = RemoveDiacritics(cleanText);

        // 4. Cortamos en tokens
        return cleanText
            .Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2)
            .ToHashSet();
    }

    private static HashSet<string> TokenizeWithSynonyms(HashSet<string> strictTokens, Dictionary<string, string[]> synonyms)
    {
        var expanded = new HashSet<string>(strictTokens);
        foreach (var t in strictTokens)
        {
            if (synonyms.TryGetValue(t, out var syns))
            {
                foreach (var s in syns) expanded.Add(s);
            }
        }
        return expanded;
    }
}
