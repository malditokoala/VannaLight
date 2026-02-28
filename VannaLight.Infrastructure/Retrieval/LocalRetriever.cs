using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;
using VannaLight.Core.Settings;

namespace VannaLight.Infrastructure.Retrieval;

public class LocalRetriever : IRetriever
{
    private readonly ISchemaStore _schemaStore;
    private readonly ITrainingStore _trainingStore;
    private readonly AppSettings _settings;

    public LocalRetriever(ISchemaStore schemaStore, ITrainingStore trainingStore, AppSettings settings)
    {
        _schemaStore = schemaStore;
        _trainingStore = trainingStore;
        _settings = settings;
    }

    public async Task<RetrievalContext> RetrieveAsync(string sqlitePath, string question, CancellationToken ct)
    {
        var queryTokens = Tokenize(question);

        // 1. Priorizar ejemplos validados históricos
        var allExamples = await _trainingStore.GetAllTrainingExamplesAsync(sqlitePath, ct);
        var rankedExamples = allExamples
            .Select(ex => new RetrievedExample(ex, CalculateScore(Tokenize(ex.Question), queryTokens)))
            .Where(r => r.Score > 0)
            .OrderByDescending(r => r.Score)
            .Take(_settings.Retrieval.TopExamples)
            .ToList();

        // 2. Recuperar documentos del esquema (RAG)
        var allSchemaDocs = await _schemaStore.GetAllSchemaDocsAsync(sqlitePath, ct);
        var rankedDocs = allSchemaDocs
            .Select(doc => new RetrievedSchemaDoc(doc, CalculateScore(Tokenize(doc.DocText), queryTokens)))
            .Where(r => r.Score > 0)
            .OrderByDescending(r => r.Score)
            .Take(_settings.Retrieval.TopSchemaDocs)
            .ToList();

        return new RetrievalContext(rankedExamples, rankedDocs);
    }

    // Heurística de coincidencia de palabras exacta (BM25 simplificado para MVP)
    private double CalculateScore(HashSet<string> documentTokens, HashSet<string> queryTokens)
    {
        return queryTokens.Count(documentTokens.Contains);
    }

    private HashSet<string> Tokenize(string text)
    {
        var charsToRemove = new[] { '.', ',', '?', '¿', '!', '¡', '(', ')', '[', ']', '-', '_' };
        var cleanText = new string(text.Where(c => !charsToRemove.Contains(c)).ToArray());

        return cleanText.ToLowerInvariant()
            .Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2) // Filtramos palabras muy cortas
            .ToHashSet();
    }
}