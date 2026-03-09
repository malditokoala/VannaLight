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
        var valueRegex = BuildRegex(field);
        var matches = valueRegex.Matches(text);
        if (matches.Count == 0) return string.Empty;

        // ✅ CAMBIO 1: encontrar TODAS las ocurrencias de anchors (no solo la primera con IndexOf)
        var anchorIdx = FindAllAnchorIndexes(text, field.AnchorLabels);
        if (anchorIdx.Count == 0) return string.Empty; // sin ancla => no es seguro extraer

        // ✅ CAMBIO 2: score contra TODOS los anchors (min distancia)
        var scoredMatches = matches.Cast<Match>()
            .Select(m => new
            {
                Match = m,
                Dist = anchorIdx.Min(a => Math.Abs(m.Index - a))
            })
            .OrderBy(x => x.Dist)
            .ToList();

        // --- MANEJO DE LISTAS (Empaque, Separadores, etc.) ---
        if (field.IsList)
        {
            if (field.PatternType == "number-unit")
            {
                var uniqueUnits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in scoredMatches.Where(x => x.Dist < 300))
                {
                    var unitRaw = item.Match.Groups["u"].Value.ToLower();
                    var unitCanon = field.UnitMap?.GetValueOrDefault(unitRaw) ?? unitRaw;
                    if (!uniqueUnits.ContainsKey(unitCanon))
                        uniqueUnits[unitCanon] = $"{item.Match.Groups["n"].Value} {unitCanon}";
                }
                return string.Join(", ", uniqueUnits.Values);
            }
            else
            {
                // Para listas de Part Numbers (como los empaques N/P)
                var uniqueParts = scoredMatches.Where(x => x.Dist < 350)
                                               .Select(x => x.Match.Value) // Atrapa el string completo (ej. 700009-0030)
                                               .Distinct()
                                               .ToList();

                return string.Join(", ", uniqueParts);
            }
            // Retorna aquí y corta la ejecución si es una lista.
        }

        // --- MANEJO DE CAMPOS SIMPLES (Resina, Molde, Estándar, etc.) ---

        // ✅ CAMBIO 3: Para campos normales, guardar el MEJOR (menor distancia) por unidad.
        var bestByUnit = new Dictionary<string, (int Dist, string Value)>(StringComparer.OrdinalIgnoreCase);

        // radio razonable (puedes subir/bajar)
        const int radius = 300;

        foreach (var item in scoredMatches.Where(x => x.Dist < radius))
        {
            string val;
            string groupingKey;

            if (field.PatternType == "number-unit")
            {
                var unitRaw = item.Match.Groups["u"].Value.ToLowerInvariant();
                var unitCanon = field.UnitMap?.GetValueOrDefault(unitRaw) ?? unitRaw;
                val = $"{item.Match.Groups["n"].Value} {unitCanon}";
                groupingKey = unitCanon; // Agrupamos por unidad (lbs, pzs)
            }
            else
            {
                // Para códigos como H-321 o 910035-0001
                val = item.Match.Value;
                groupingKey = "default"; // Solo guardamos el mejor de todos
            }

            if (!bestByUnit.TryGetValue(groupingKey, out var cur) || item.Dist < cur.Dist)
                bestByUnit[groupingKey] = (item.Dist, val);
        }

        // Orden por cercanía al anchor (más cercano primero)
        return string.Join(", ", bestByUnit.Values.OrderBy(x => x.Dist).Select(x => x.Value));
    }

    private static List<int> FindAllAnchorIndexes(string text, List<string> anchors)
    {
        var hits = new List<int>();
        if (string.IsNullOrEmpty(text) || anchors is null || anchors.Count == 0) return hits;

        foreach (var anchor in anchors)
        {
            if (string.IsNullOrWhiteSpace(anchor)) continue;

            var start = 0;
            while (true)
            {
                var idx = text.IndexOf(anchor, start, StringComparison.OrdinalIgnoreCase);
                if (idx < 0) break;

                hits.Add(idx);
                start = idx + Math.Max(1, anchor.Length);
            }
        }

        return hits;
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