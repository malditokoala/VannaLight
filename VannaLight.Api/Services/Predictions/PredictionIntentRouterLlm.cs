using System;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Api.Services.Predictions;

/// <summary>
/// Enrutador semantico que discrimina entre consultas SQL y predicciones futuras.
/// Mantiene compatibilidad con el caso industrial, pero ya acepta dominios genericos
/// como ventas, demanda o volumen por producto/cliente/pais.
/// </summary>
public class PredictionIntentRouterLlm : IPredictionIntentRouter
{
    private readonly ILlmClient _llm;

    private static readonly string[] FutureMarkers =
    [
        "pronostico", "pronóstico", "predec", "forecast", "estim",
        "mañana", "manana", "próximo", "proximo", "siguiente", "next",
        "mes siguiente", "próximo mes", "proximo mes", "cierre"
    ];

    private static readonly string[] PresentOrPastMarkers =
    [
        "hoy", "ayer", "actual", "actuales", "acumulado", "en curso",
        "va", "lleva", "registrado", "pasado", "último", "ultimo", "este mes"
    ];

    public PredictionIntentRouterLlm(ILlmClient llm)
    {
        _llm = llm;
    }

    public async Task<PredictionIntent> ParseAsync(string question, CancellationToken ct)
    {
        var heuristic = TryFastParse(question);
        if (heuristic is not null)
            return heuristic;

        var prompt = $@"
<|im_start|>system
Eres el enrutador analitico de VannaLight.
Tu objetivo es discriminar entre consultas de DATOS REALES y PRONOSTICOS FUTUROS (ML) para cualquier dominio de negocio.

El sistema puede trabajar con series como:
- numeros de parte
- productos
- categorias
- clientes
- paises
- maquinas
- cualquier otra clave de serie temporal configurable

REGLA DE ORO:
- DATOS EN CURSO O PASADOS -> IsPredictionRequest = false. Ejemplos: ""hoy"", ""ayer"", ""este turno"", ""cuanto lleva"", ""acumulado"", ""actual"", ""este mes"".
- PRONOSTICOS FUTUROS -> IsPredictionRequest = true. Ejemplos: ""pronostico cierre de turno"", ""siguiente turno"", ""mañana"", ""proximo mes"", ""forecast de ventas del proximo mes"".

Mapea el periodo futuro a uno de estos ""PredictionTarget"":
- ""EndOfCurrentShift"" = cierre del bucket o periodo activo actual
- ""NextShift"" = siguiente bucket o siguiente periodo corto disponible
- ""Tomorrow"" = siguiente dia completo
- ""NextMonth"" = siguiente mes completo

Extrae ""EntityName"" solo si la pregunta identifica claramente una serie concreta.
Extrae tambien:
- ""MetricKey"" = metrica objetivo inferida
- ""SeriesType"" = tipo de serie, por ejemplo part, product, category, customer, ship_country, employee, press, department

Metricas esperadas del sistema:
- scrap_qty
- produced_qty
- downtime_minutes
- units_sold
- net_sales
- order_count

Si la pregunta es un forecast agregado sin serie explicita y no puedes inferir una sola serie concreta, deja EntityName = null.

Si el usuario pide predecir periodos ambiguos o en curso, marca IsPredictionRequest = false y pon en UnsupportedReason: ""Datos en curso o periodo ambiguo. Se procesara via SQL normal.""

Retorna SOLO un JSON valido:
{{
  ""IsPredictionRequest"": true/false,
  ""EntityName"": ""Clave de serie o null"",
  ""MetricKey"": ""scrap_qty, units_sold, net_sales, order_count, produced_qty, downtime_minutes o null"",
  ""SeriesType"": ""part, product, category, customer, ship_country, employee, press, department o null"",
  ""PredictionTarget"": ""EndOfCurrentShift, NextShift, Tomorrow o NextMonth o null"",
  ""UnsupportedReason"": ""Motivo de rechazo o null""
}}
<|im_end|>
<|im_start|>user
{question}
<|im_end|>
<|im_start|>assistant
{{";

        var rawResponse = "{" + await _llm.CompleteAsync(prompt, ct);
        var cleanJson = ExtractValidJson(rawResponse);

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var parsed = JsonSerializer.Deserialize<PredictionIntent>(cleanJson, options)
                         ?? new PredictionIntent { IsPredictionRequest = false };
            NormalizeIntent(parsed);
            return parsed;
        }
        catch
        {
            return new PredictionIntent
            {
                IsPredictionRequest = false,
                UnsupportedReason = "Error al interpretar la intencion de la pregunta."
            };
        }
    }

    private PredictionIntent? TryFastParse(string question)
    {
        var normalized = NormalizeQuestion(question);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        var hasFuture = FutureMarkers.Any(marker => normalized.Contains(marker, StringComparison.Ordinal));
        var hasPresentOrPast = PresentOrPastMarkers.Any(marker => normalized.Contains(marker, StringComparison.Ordinal));

        if (!hasFuture)
        {
            return hasPresentOrPast
                ? new PredictionIntent
                {
                    IsPredictionRequest = false,
                    UnsupportedReason = "Datos en curso o periodo ambiguo. Se procesara via SQL normal."
                }
                : null;
        }

        return new PredictionIntent
        {
            IsPredictionRequest = true,
            EntityName = TryExtractEntity(question),
            MetricKey = ResolveMetricKey(normalized),
            SeriesType = ResolveSeriesType(normalized),
            PredictionTarget = ResolvePredictionTarget(normalized),
            UnsupportedReason = null
        };
    }

    private static string? TryExtractEntity(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return null;

        var quoted = Regex.Match(question, "\"([^\"]+)\"|'([^']+)'");
        if (quoted.Success)
        {
            var direct = quoted.Groups[1].Success ? quoted.Groups[1].Value : quoted.Groups[2].Value;
            return string.IsNullOrWhiteSpace(direct) ? null : direct.Trim();
        }

        string[] patterns =
        [
            @"(?:producto|productid|sku|serie|parte|n/?p|país|pais|country|cliente|customer|categoria|categoría|empleado|employee|prensa|press|departamento|department)\s+([A-Za-z0-9\-_\.]+)",
            @"(?:de|del|para|por)\s+([A-Za-z0-9\-_\.]{2,})\s*(?:mañana|manana|próximo|proximo|siguiente|next|$)"
        ];

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(question, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success)
                continue;

            var candidate = match.Groups[1].Value.Trim().Trim(',', '.', ';', ':');
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate;
        }

        return null;
    }

    private static string ResolvePredictionTarget(string normalized)
    {
        if (normalized.Contains("next month", StringComparison.Ordinal) ||
            normalized.Contains("próximo mes", StringComparison.Ordinal) ||
            normalized.Contains("proximo mes", StringComparison.Ordinal) ||
            normalized.Contains("mes siguiente", StringComparison.Ordinal))
        {
            return "NextMonth";
        }

        if (normalized.Contains("mañana", StringComparison.Ordinal) ||
            normalized.Contains("manana", StringComparison.Ordinal) ||
            normalized.Contains("tomorrow", StringComparison.Ordinal))
        {
            return "Tomorrow";
        }

        if (normalized.Contains("siguiente turno", StringComparison.Ordinal) ||
            normalized.Contains("proximo turno", StringComparison.Ordinal) ||
            normalized.Contains("próximo turno", StringComparison.Ordinal) ||
            normalized.Contains("next shift", StringComparison.Ordinal) ||
            normalized.Contains("siguiente bucket", StringComparison.Ordinal) ||
            normalized.Contains("próximo bucket", StringComparison.Ordinal))
        {
            return "NextShift";
        }

        return "EndOfCurrentShift";
    }

    private static string? ResolveMetricKey(string normalized)
    {
        if (normalized.Contains("downtime", StringComparison.Ordinal) ||
            normalized.Contains("paro", StringComparison.Ordinal) ||
            normalized.Contains("tiempo muerto", StringComparison.Ordinal))
        {
            return "downtime_minutes";
        }

        if (normalized.Contains("scrap", StringComparison.Ordinal) ||
            normalized.Contains("rechazo", StringComparison.Ordinal) ||
            normalized.Contains("desperdicio", StringComparison.Ordinal))
        {
            return "scrap_qty";
        }

        if (normalized.Contains("produccion", StringComparison.Ordinal) ||
            normalized.Contains("producción", StringComparison.Ordinal) ||
            normalized.Contains("producido", StringComparison.Ordinal))
        {
            return "produced_qty";
        }

        if (normalized.Contains("venta", StringComparison.Ordinal) ||
            normalized.Contains("ventas", StringComparison.Ordinal) ||
            normalized.Contains("revenue", StringComparison.Ordinal) ||
            normalized.Contains("ingreso", StringComparison.Ordinal) ||
            normalized.Contains("sales", StringComparison.Ordinal))
        {
            return "net_sales";
        }

        if (normalized.Contains("orden", StringComparison.Ordinal) ||
            normalized.Contains("pedido", StringComparison.Ordinal) ||
            normalized.Contains("orders", StringComparison.Ordinal))
        {
            return "order_count";
        }

        if (normalized.Contains("unidad", StringComparison.Ordinal) ||
            normalized.Contains("unidades", StringComparison.Ordinal) ||
            normalized.Contains("cantidad", StringComparison.Ordinal) ||
            normalized.Contains("demanda", StringComparison.Ordinal) ||
            normalized.Contains("quantity", StringComparison.Ordinal))
        {
            return "units_sold";
        }

        return null;
    }

    private static string? ResolveSeriesType(string normalized)
    {
        if (normalized.Contains("product", StringComparison.Ordinal) ||
            normalized.Contains("producto", StringComparison.Ordinal) ||
            normalized.Contains("sku", StringComparison.Ordinal))
        {
            return "product";
        }

        if (normalized.Contains("categoria", StringComparison.Ordinal) ||
            normalized.Contains("categoría", StringComparison.Ordinal) ||
            normalized.Contains("category", StringComparison.Ordinal))
        {
            return "category";
        }

        if (normalized.Contains("cliente", StringComparison.Ordinal) ||
            normalized.Contains("customer", StringComparison.Ordinal))
        {
            return "customer";
        }

        if (normalized.Contains("pais", StringComparison.Ordinal) ||
            normalized.Contains("país", StringComparison.Ordinal) ||
            normalized.Contains("country", StringComparison.Ordinal))
        {
            return "ship_country";
        }

        if (normalized.Contains("empleado", StringComparison.Ordinal) ||
            normalized.Contains("employee", StringComparison.Ordinal))
        {
            return "employee";
        }

        if (normalized.Contains("prensa", StringComparison.Ordinal) ||
            normalized.Contains("press", StringComparison.Ordinal))
        {
            return "press";
        }

        if (normalized.Contains("departamento", StringComparison.Ordinal) ||
            normalized.Contains("department", StringComparison.Ordinal))
        {
            return "department";
        }

        if (normalized.Contains("parte", StringComparison.Ordinal) ||
            normalized.Contains("n/p", StringComparison.Ordinal) ||
            normalized.Contains("part", StringComparison.Ordinal))
        {
            return "part";
        }

        if (normalized.Contains("serie", StringComparison.Ordinal))
        {
            return "series";
        }

        return null;
    }

    private static string NormalizeQuestion(string? question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return string.Empty;

        return question.Trim().ToLowerInvariant();
    }

    private static void NormalizeIntent(PredictionIntent intent)
    {
        intent.EntityName = string.IsNullOrWhiteSpace(intent.EntityName) ? null : intent.EntityName.Trim();
        intent.MetricKey = string.IsNullOrWhiteSpace(intent.MetricKey) ? null : intent.MetricKey.Trim();
        intent.SeriesType = string.IsNullOrWhiteSpace(intent.SeriesType) ? null : intent.SeriesType.Trim();
        intent.PredictionTarget = string.IsNullOrWhiteSpace(intent.PredictionTarget) ? null : intent.PredictionTarget.Trim();

        if (intent.IsPredictionRequest && string.IsNullOrWhiteSpace(intent.PredictionTarget))
            intent.PredictionTarget = "EndOfCurrentShift";
    }

    private string ExtractValidJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "{}";
        var match = Regex.Match(text, @"\{[\s\S]*\}");
        return match.Success ? match.Value : "{}";
    }
}
