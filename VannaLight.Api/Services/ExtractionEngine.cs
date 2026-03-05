using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace VannaLight.Api.Services;

// --- MODELOS DEL ESQUEMA ---
public record DocTypeSchema(string TypeId, string DisplayName, ScoringConfig Scoring, List<FieldDef> Fields);
public record ScoringConfig(List<string> BoostLabels);
public record FieldDef(string Key, string DisplayLabel, int Order, List<string> Tags, string PatternType,
    List<string> AnchorLabels, List<string>? ExpectedUnits = null, Dictionary<string, string>? UnitMap = null,
    string? Prefix = null, bool IsList = false);

// --- EL MOTOR GENÉRICO ---
public static class ExtractionEngine
{
    public static Dictionary<string, string> ExtractAll(DocTypeSchema schema, string text)
    {
        var facts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text)) return facts;

        // Normalizar una sola vez
        text = Regex.Replace(text.Replace('\n', ' ').Replace('\r', ' '), @"\s{2,}", " ");

        foreach (var field in schema.Fields)
        {
            var result = ExtractField(field, text);
            if (!string.IsNullOrEmpty(result))
                facts[field.Key] = result;
        }
        return facts;
    }

    private static string ExtractField(FieldDef field, string text)
    {
        Regex valueRegex = BuildRegex(field);
        var matches = valueRegex.Matches(text);
        if (matches.Count == 0) return string.Empty;

        // Encontrar los índices de las anclas en el texto
        var anchorIdx = field.AnchorLabels
            .Select(a => text.IndexOf(a, StringComparison.OrdinalIgnoreCase))
            .Where(i => i >= 0).ToArray();

        if (anchorIdx.Length == 0) return string.Empty; // Si no hay ancla, no es seguro extraer

        // Magia: Elegir los matches más cercanos al ancla (Distancia Absoluta)
        var scoredMatches = matches.Cast<Match>()
            .Select(m => new { Match = m, Dist = anchorIdx.Min(a => Math.Abs(m.Index - a)) })
            .OrderBy(x => x.Dist)
            .ToList();

        if (!field.IsList)
        {
            // Escalar: Devolvemos el más cercano formateado
            var best = scoredMatches.First().Match;
            return FormatMatch(best, field);
        }
        else
        {
            // Lista: Agrupamos las unidades cercanas al ancla
            var uniqueUnits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Tomamos los que estén en un radio razonable (ej. 300 caracteres del ancla)
            foreach (var item in scoredMatches.Where(x => x.Dist < 300))
            {
                var unitRaw = item.Match.Groups["u"].Value.ToLower();
                var unitCanon = field.UnitMap?.GetValueOrDefault(unitRaw) ?? unitRaw;

                if (!uniqueUnits.ContainsKey(unitCanon))
                    uniqueUnits[unitCanon] = $"{item.Match.Groups["n"].Value} {unitCanon}";
            }
            return string.Join(", ", uniqueUnits.Values);
        }
    }

    private static Regex BuildRegex(FieldDef field)
    {
        // Construcción segura y dinámica de Regex con Timeout (Evita ReDoS)
        var options = RegexOptions.IgnoreCase | RegexOptions.Compiled;
        var timeout = TimeSpan.FromMilliseconds(50);

        return field.PatternType switch
        {
            "number-unit" => new Regex($@"(?<!\d)(?<n>\d{{1,4}}(?:[.,]\d{{1,3}})?)[\s\-:;,.]*?(?<u>{string.Join("|", field.ExpectedUnits!)})", options, timeout),
            "code-prefix" => new Regex($@"\b(?<v>{field.Prefix}\d{{2,4}}(?:-\d+)?)\b", options, timeout),
            "part-number" => new Regex($@"\b(?<v>{field.Prefix}\d{{4,}}-\d{{3,}})\b", options, timeout),
            _ => throw new InvalidOperationException($"Pattern desconocido: {field.PatternType}")
        };
    }

    private static string FormatMatch(Match m, FieldDef field)
    {
        if (field.PatternType == "number-unit")
        {
            var unitRaw = m.Groups["u"].Value.ToLower();
            var unitCanon = field.UnitMap?.GetValueOrDefault(unitRaw) ?? unitRaw;
            return $"{m.Groups["n"].Value} {unitCanon}";
        }
        return m.Groups["v"].Value;
    }
}