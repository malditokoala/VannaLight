using System.Text.RegularExpressions;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Infrastructure.Retrieval;

public class PatternMatcherService : IPatternMatcherService
{
    public PatternMatchResult Match(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return new PatternMatchResult();

        var q = question.Trim().ToLowerInvariant();
        int topN = ExtractTopN(q);

        // 1) Top scrap por prensa
        if (ContainsAny(q, "scrap") &&
            ContainsAny(q, "prensa", "prensas") &&
            ContainsAny(q, "más", "mayor", "top"))
        {
            return new PatternMatchResult
            {
                IsMatch = true,
                PatternKey = "top_scrap_by_press",
                IntentName = "Top scrap por prensa",
                TopN = topN > 0 ? topN : 5,
                Metric = PatternMetric.ScrapQty,
                Dimension = PatternDimension.Press,
                TimeScope = DetectTimeScope(q)
            };
        }

        // 2) Producción total
        if (ContainsAny(q, "producción total", "produccion total", "total producido", "producción"))
        {
            if (!ContainsAny(q, "prensa", "prensas", "molde", "moldes", "falla", "fallas", "departamento"))
            {
                return new PatternMatchResult
                {
                    IsMatch = true,
                    PatternKey = "total_production",
                    IntentName = "Producción total",
                    Metric = PatternMetric.ProducedQty,
                    Dimension = PatternDimension.Unknown,
                    TimeScope = DetectTimeScope(q)
                };
            }
        }

        // 3) Downtime por falla
        if (ContainsAny(q, "downtime", "tiempo caído", "tiempo caido") &&
            ContainsAny(q, "falla", "fallas"))
        {
            return new PatternMatchResult
            {
                IsMatch = true,
                PatternKey = "top_downtime_by_failure",
                IntentName = "Downtime por falla",
                TopN = topN > 0 ? topN : 5,
                Metric = PatternMetric.DownTimeMinutes,
                Dimension = PatternDimension.Failure,
                TimeScope = DetectTimeScope(q)
            };
        }

        // 4) Downtime por departamento
        if (ContainsAny(q, "downtime", "tiempo caído", "tiempo caido") &&
            ContainsAny(q, "departamento", "departamentos"))
        {
            return new PatternMatchResult
            {
                IsMatch = true,
                PatternKey = "downtime_by_department",
                IntentName = "Downtime por departamento",
                TopN = topN > 0 ? topN : 0,
                Metric = PatternMetric.DownTimeMinutes,
                Dimension = PatternDimension.Department,
                TimeScope = DetectTimeScope(q)
            };
        }

        // 5) Top moldes por scrap cost
        if (ContainsAny(q, "scrap cost", "costo de scrap", "coste de scrap") &&
            ContainsAny(q, "molde", "moldes"))
        {
            return new PatternMatchResult
            {
                IsMatch = true,
                PatternKey = "top_scrap_cost_by_mold",
                IntentName = "Top moldes por scrap cost",
                TopN = topN > 0 ? topN : 5,
                Metric = PatternMetric.ScrapCost,
                Dimension = PatternDimension.Mold,
                TimeScope = DetectTimeScope(q)
            };
        }

        return new PatternMatchResult();
    }

    private static int ExtractTopN(string q)
    {
        var match = Regex.Match(q, @"\b(\d{1,3})\b");
        if (match.Success && int.TryParse(match.Value, out var n) && n > 0)
            return n;

        return 0;
    }

    private static PatternTimeScope DetectTimeScope(string q)
    {
        if (ContainsAny(q, "turno actual", "del turno", "turno"))
            return PatternTimeScope.CurrentShift;

        if (ContainsAny(q, "hoy", "día de hoy", "dia de hoy"))
            return PatternTimeScope.Today;

        if (ContainsAny(q, "ayer"))
            return PatternTimeScope.Yesterday;

        if (ContainsAny(q, "semana actual", "esta semana", "de la semana"))
            return PatternTimeScope.CurrentWeek;

        if (ContainsAny(q, "mes actual", "este mes", "del mes"))
            return PatternTimeScope.CurrentMonth;

        return PatternTimeScope.Unknown;
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        return values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));
    }
}
