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
    private readonly IMemoryCache _cache;

    private sealed record DictRow(string Term, string MappedTokens);

    public LocalRetriever(
        ISchemaStore schemaStore,
        ITrainingStore trainingStore,
        AppSettings settings,
        IMemoryCache cache)
    {
        _schemaStore = schemaStore;
        _trainingStore = trainingStore;
        _settings = settings;
        _cache = cache;
    }

    public async Task<RetrievalContext> RetrieveAsync(string sqlitePath, string question, CancellationToken ct)
    {
        var synonyms = await LoadSynonymsAsync(sqlitePath, ct);

        var strictQueryTokens = TokenizeStrict(question);
        var expandedQueryTokens = TokenizeWithSynonyms(strictQueryTokens, synonyms);

        var allExamples = await _trainingStore.GetAllTrainingExamplesAsync(sqlitePath, ct);
        var rankedExamples = allExamples
            .Select(ex => new RetrievedExample(ex, CalculateScore(TokenizeStrict(ex.Question), strictQueryTokens)))
            .Where(r => r.Score > 0)
            .OrderByDescending(r => r.Score)
            .Take(_settings.Retrieval.TopExamples)
            .ToList();

        var allSchemaDocs = await _schemaStore.GetAllSchemaDocsAsync(sqlitePath, ct);
        var rankedSchemaDocs = allSchemaDocs
            .Select(doc => new RetrievedSchemaDoc(doc, CalculateScore(TokenizeStrict(doc.DocText), expandedQueryTokens)))
            .OrderByDescending(r => r.Score)
            .ToList();

        var topSchemaDocs = rankedSchemaDocs
            .Where(r => r.Score > 0)
            .Take(_settings.Retrieval.TopSchemaDocs)
            .ToList();

        // Fallback inteligente
        if (topSchemaDocs.Count == 0)
        {
            var fallbackN = _settings.Retrieval.FallbackSchemaDocs > 0
                ? _settings.Retrieval.FallbackSchemaDocs
                : _settings.Retrieval.TopSchemaDocs;

            topSchemaDocs = rankedSchemaDocs.Take(fallbackN).ToList();
        }

        return new RetrievalContext(rankedExamples, topSchemaDocs);
    }

    private async Task<Dictionary<string, string[]>> LoadSynonymsAsync(string sqlitePath, CancellationToken ct)
    {
        // Caché dinámico por BD y Dominio
        var domain = _settings.Retrieval.Domain ?? "global";
        var cacheKey = $"BusinessSynonyms::{sqlitePath}::{domain}";

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
                new CommandDefinition(sql, new { Domain = domain }, cancellationToken: ct));

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