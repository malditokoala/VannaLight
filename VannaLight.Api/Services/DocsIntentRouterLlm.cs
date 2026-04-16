using System;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VannaLight.Api.Contracts;
using VannaLight.Core.Abstractions;

namespace VannaLight.Api.Services;

public interface IDocsIntentRouter
{
    Task<DocsIntent> ParseAsync(string question, DocTypeSchema schema, CancellationToken ct);
}

public class DocsIntentRouterLlm : IDocsIntentRouter
{
    private readonly ILlmClient _llm;

    public DocsIntentRouterLlm(ILlmClient llm)
    {
        _llm = llm;
    }

    public async Task<DocsIntent> ParseAsync(string question, DocTypeSchema schema, CancellationToken ct)
    {
        var heuristic = TryParseHeuristic(question, schema);
        if (heuristic is not null)
            return heuristic;

        // 1. Contexto: Convertimos el JSON Schema en una lista legible para el LLM
        var fieldsContext = string.Join("\n", schema.Fields.Select(f =>
            $"- Key: '{f.Key}', Label: '{f.DisplayLabel}', Tags: [{string.Join(", ", f.Tags ?? new())}]"));

        // 2. Prompt Estricto (KISS)
        var prompt = $@"
Eres un enrutador semántico. Devuelve SOLO un objeto JSON válido. No agregues texto antes ni después.
Campos disponibles:
{fieldsContext}

Reglas:
1. 'RequestedFields' es un array de strings con los 'Key' exactos de la lista superior que responden a la pregunta.
2. 'Periodo' solo puede ser 'Turno', '2 Horas' o null.
3. Si la pregunta es muy general (ej. 'dame la ficha'), pon 'ShowAll': true.

Pregunta del usuario: {question}
Respuesta JSON:";

        // 3. Ejecución
        var rawResponse = await _llm.CompleteAsync(prompt, ct);

        // 4. Extracción blindada del JSON (Ignora ```json y texto extra)
        var cleanJson = ExtractValidJson(rawResponse);

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var intent = JsonSerializer.Deserialize<DocsIntent>(cleanJson, options) ?? new DocsIntent { ShowAll = true };

            // Validación dura: Evitar que el LLM invente keys que no existen
            if (intent.RequestedFields != null && schema.Fields != null)
            {
                var validKeys = schema.Fields.Select(f => f.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
                intent.RequestedFields = intent.RequestedFields.Where(k => validKeys.Contains(k)).ToList();
            }

            // Llenado de booleanos Legacy para que no se rompa el viejo flujo
            intent.WantsResina = intent.RequestedFields?.Any(f => f.Contains("Resina", StringComparison.OrdinalIgnoreCase)) ?? false;
            intent.WantsEmpaque = intent.RequestedFields?.Any(f => f.Contains("Empaque", StringComparison.OrdinalIgnoreCase)) ?? false;

            return intent;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ROUTER ERROR] Fallo al parsear JSON del LLM. Error: {ex.Message}. JSON limpio: {cleanJson}");
            // Fallback: Mostrar todo para no dejar ciego al usuario
            return new DocsIntent { ShowAll = true, RequestedFields = schema.Fields?.Select(f => f.Key).ToList() ?? new() };
        }
    }

    // Extractor robusto a prueba de balas (Algoritmo de balanceo de llaves)
    private string ExtractValidJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "{}";

        int startIndex = text.IndexOf('{');
        if (startIndex == -1) return "{}"; // Si no hay ni una llave, devuelve JSON vacío

        int braceCount = 0;
        for (int i = startIndex; i < text.Length; i++)
        {
            if (text[i] == '{') braceCount++;
            else if (text[i] == '}') braceCount--;

            // Cuando el contador vuelve a 0, encontramos el final del primer bloque JSON perfecto
            if (braceCount == 0)
            {
                return text.Substring(startIndex, i - startIndex + 1);
            }
        }

        return "{}"; // Fallback si las llaves están desbalanceadas
    }

    private static DocsIntent? TryParseHeuristic(string question, DocTypeSchema schema)
    {
        if (string.IsNullOrWhiteSpace(question) || schema.Fields is null || schema.Fields.Count == 0)
            return null;

        var q = Normalize(question);
        var validKeys = schema.Fields.Select(f => f.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (q.Contains("ficha") || q.Contains("todo") || q.Contains("completo"))
        {
            return new DocsIntent
            {
                ShowAll = true,
                RequestedFields = schema.Fields.Select(f => f.Key).ToList()
            };
        }

        string? periodo = null;
        if (q.Contains("turno"))
            periodo = "Turno";
        else if (q.Contains("2 horas") || q.Contains("dos horas"))
            periodo = "2 Horas";

        var requested = new List<string>();

        if (q.Contains("molde"))
            AddIfPresent(requested, validKeys, "Molde");

        if (q.Contains("resina"))
        {
            if (periodo == "Turno")
                AddIfPresent(requested, validKeys, "ResinaTurno");
            else if (periodo == "2 Horas")
                AddIfPresent(requested, validKeys, "Resina2Horas");
            else
                AddIfPresent(requested, validKeys, "ResinaNP", "ResinaTurno", "Resina2Horas");
        }

        if (q.Contains("empaque"))
        {
            if (periodo == "Turno")
                AddIfPresent(requested, validKeys, "EmpaqueTurno");
            else if (periodo == "2 Horas")
                AddIfPresent(requested, validKeys, "Empaque2Horas");
            else if (LooksLikePartNumberQuestion(q))
                AddIfPresent(requested, validKeys, "EmpaqueNP", "Empaque");
            else
                AddIfPresent(requested, validKeys, "Empaque", "EmpaqueTurno", "Empaque2Horas", "EmpaqueNP");
        }

        if (requested.Count == 0)
            return null;

        return new DocsIntent
        {
            Periodo = periodo,
            RequestedFields = requested.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            WantsResina = requested.Any(f => f.Contains("Resina", StringComparison.OrdinalIgnoreCase)),
            WantsEmpaque = requested.Any(f => f.Contains("Empaque", StringComparison.OrdinalIgnoreCase))
        };
    }

    private static bool LooksLikePartNumberQuestion(string normalizedQuestion)
        => Regex.IsMatch(normalizedQuestion, @"\b\d{4,}-\d{3,}\b", RegexOptions.IgnoreCase);

    private static string Normalize(string input)
    {
        var lower = input.ToLowerInvariant();
        return Regex.Replace(lower, @"\s+", " ").Trim();
    }

    private static void AddIfPresent(List<string> requested, HashSet<string> validKeys, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (validKeys.Contains(candidate))
                requested.Add(candidate);
        }
    }
}
