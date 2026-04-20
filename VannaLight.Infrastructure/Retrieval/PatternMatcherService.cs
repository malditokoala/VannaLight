using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Infrastructure.Retrieval;

public sealed class PatternMatcherService : IPatternMatcherService
{
    private const double IntentEvidenceThreshold = 10d;
    private const double RouteEvidenceThreshold = 14d;

    private readonly IQueryPatternStore _patternStore;
    private readonly IQueryPatternTermStore _termStore;

    public PatternMatcherService(
        IQueryPatternStore patternStore,
        IQueryPatternTermStore termStore)
    {
        _patternStore = patternStore;
        _termStore = termStore;
    }

    public async Task<PatternMatchResult> MatchAsync(string question, string? domain, CancellationToken ct = default)
    {
        var evaluation = await EvaluateBestPatternAsync(question, domain, ct);
        var builtIn = TryBuiltInMatch(question, domain);
        if (evaluation is null)
            return builtIn;

        var evaluated = BuildResult(evaluation);
        if (builtIn.IsMatch && !evaluated.IsMatch)
            return builtIn;

        return evaluated;
    }

    public async Task<string?> InferIntentNameAsync(string question, string? domain, CancellationToken ct = default)
    {
        var evaluation = await EvaluateBestPatternAsync(question, domain, ct);
        var builtIn = TryBuiltInMatch(question, domain);
        if (evaluation is null)
            return string.IsNullOrWhiteSpace(builtIn.IntentName) ? null : builtIn.IntentName;

        if (!evaluation.HasIntentEvidence && !string.IsNullOrWhiteSpace(builtIn.IntentName))
            return builtIn.IntentName;

        if (!evaluation.HasIntentEvidence)
            return null;

        return evaluation.Pattern.IntentName;
    }

    private async Task<PatternEvaluation?> EvaluateBestPatternAsync(string question, string? domain, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(domain))
            return null;

        var activePatterns = await _patternStore.GetActiveAsync(domain, ct);
        if (activePatterns.Count == 0)
            return null;

        var patternIds = activePatterns
            .Select(x => x.Id)
            .Where(x => x > 0)
            .ToArray();

        if (patternIds.Length == 0)
            return null;

        var terms = await _termStore.GetActiveByPatternIdsAsync(patternIds, ct);
        var termLookup = terms
            .GroupBy(x => x.PatternId)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<QueryPatternTerm>)x.ToList());

        var normalizedQuestion = NormalizeText(question);
        if (string.IsNullOrWhiteSpace(normalizedQuestion))
            return null;

        PatternEvaluation? best = null;

        foreach (var pattern in activePatterns)
        {
            if (!termLookup.TryGetValue(pattern.Id, out var patternTerms) || patternTerms.Count == 0)
                continue;

            var evaluation = EvaluatePattern(pattern, patternTerms, normalizedQuestion, question);
            if (evaluation is null)
                continue;

            if (best is null || IsBetter(evaluation, best))
                best = evaluation;
        }

        return best;
    }

    private PatternEvaluation? EvaluatePattern(
        QueryPattern pattern,
        IReadOnlyList<QueryPatternTerm> terms,
        string normalizedQuestion,
        string rawQuestion)
    {
        var requiredTerms = terms.Where(x => x.IsRequired).ToList();
        var matchedTerms = new List<QueryPatternTerm>(terms.Count);
        var matchedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var exactMatchCount = 0;
        var requiredMatchCount = 0;

        foreach (var term in terms)
        {
            if (!IsTermMatch(normalizedQuestion, term))
                continue;

            matchedTerms.Add(term);
            matchedGroups.Add(NormalizeKey(term.TermGroup));

            if (string.Equals(term.MatchMode, "exact", StringComparison.OrdinalIgnoreCase))
                exactMatchCount++;

            if (term.IsRequired)
                requiredMatchCount++;
        }

        if (matchedTerms.Count == 0)
            return null;

        var requiredSatisfied = requiredTerms.Count == 0 || requiredMatchCount == requiredTerms.Count;
        var optionalMatchCount = matchedTerms.Count - requiredMatchCount;
        var missingRequiredCount = Math.Max(0, requiredTerms.Count - requiredMatchCount);
        var groupCount = matchedGroups.Count;

        var score =
            (requiredMatchCount * 10d) +
            (optionalMatchCount * 4d) +
            (exactMatchCount * 2d) +
            (groupCount * 1.5d) -
            (missingRequiredCount * 8d);

        var hasIntentEvidence =
            score >= IntentEvidenceThreshold &&
            !string.IsNullOrWhiteSpace(pattern.IntentName) &&
            (matchedTerms.Count >= 2 || requiredSatisfied);

        var isStrongRouteMatch =
            HasRouteTemplate(pattern) &&
            requiredSatisfied &&
            requiredMatchCount > 0 &&
            groupCount >= 2 &&
            score >= RouteEvidenceThreshold;

        return new PatternEvaluation(
            pattern,
            score,
            requiredMatchCount,
            optionalMatchCount,
            exactMatchCount,
            matchedTerms.Count,
            groupCount,
            requiredSatisfied,
            hasIntentEvidence,
            isStrongRouteMatch,
            ExtractTopN(rawQuestion),
            ResolveTimeScope(rawQuestion, pattern, matchedTerms));
    }

    private PatternMatchResult BuildResult(PatternEvaluation evaluation)
    {
        var pattern = evaluation.Pattern;

        return new PatternMatchResult
        {
            IsMatch = evaluation.IsStrongRouteMatch,
            PatternKey = evaluation.IsStrongRouteMatch ? pattern.PatternKey : string.Empty,
            IntentName = evaluation.HasIntentEvidence ? pattern.IntentName : string.Empty,
            SqlTemplate = evaluation.IsStrongRouteMatch ? pattern.SqlTemplate : string.Empty,
            TopN = evaluation.ExtractedTopN > 0
                ? evaluation.ExtractedTopN
                : pattern.DefaultTopN.GetValueOrDefault(),
            Metric = ParseMetric(pattern.MetricKey),
            Dimension = ParseDimension(pattern.DimensionKey),
            TimeScope = evaluation.ResolvedTimeScope
        };
    }

    private static bool IsBetter(PatternEvaluation candidate, PatternEvaluation current)
    {
        var compare = candidate.Score.CompareTo(current.Score);
        if (compare != 0)
            return compare > 0;

        compare = current.Pattern.Priority.CompareTo(candidate.Pattern.Priority);
        if (compare != 0)
            return compare > 0;

        compare = candidate.RequiredMatchCount.CompareTo(current.RequiredMatchCount);
        if (compare != 0)
            return compare > 0;

        compare = candidate.OptionalMatchCount.CompareTo(current.OptionalMatchCount);
        if (compare != 0)
            return compare > 0;

        return candidate.Pattern.Id < current.Pattern.Id;
    }

    private static bool HasRouteTemplate(QueryPattern pattern)
    {
        return !string.IsNullOrWhiteSpace(pattern.SqlTemplate);
    }

    private static bool IsTermMatch(string normalizedQuestion, QueryPatternTerm term)
    {
        var normalizedTerm = NormalizeText(term.Term);
        if (string.IsNullOrWhiteSpace(normalizedTerm))
            return false;

        return string.Equals(term.MatchMode, "exact", StringComparison.OrdinalIgnoreCase)
            ? ContainsExactPhrase(normalizedQuestion, normalizedTerm)
            : normalizedQuestion.Contains(normalizedTerm, StringComparison.Ordinal);
    }

    private static bool ContainsExactPhrase(string normalizedQuestion, string normalizedTerm)
    {
        var paddedQuestion = $" {normalizedQuestion} ";
        var paddedTerm = $" {normalizedTerm} ";
        return paddedQuestion.Contains(paddedTerm, StringComparison.Ordinal);
    }

    private static int ExtractTopN(string question)
    {
        var match = Regex.Match(question, @"\b(\d{1,3})\b");
        if (match.Success && int.TryParse(match.Value, out var n) && n > 0)
            return n;

        return 0;
    }

    private static PatternTimeScope ResolveTimeScope(
        string question,
        QueryPattern pattern,
        IReadOnlyCollection<QueryPatternTerm> matchedTerms)
    {
        var explicitScope = ResolveTimeScopeFromTerms(matchedTerms);
        if (explicitScope != PatternTimeScope.Unknown)
            return explicitScope;

        return ParseTimeScope(pattern.DefaultTimeScopeKey);
    }

    private static PatternTimeScope ResolveTimeScopeFromTerms(IReadOnlyCollection<QueryPatternTerm> matchedTerms)
    {
        foreach (var term in matchedTerms)
        {
            var scope = ParseTimeScopeTermGroup(term.TermGroup);
            if (scope != PatternTimeScope.Unknown)
                return scope;
        }

        return PatternTimeScope.Unknown;
    }

    private static PatternTimeScope ParseTimeScopeTermGroup(string? termGroup)
    {
        return NormalizeKey(termGroup) switch
        {
            "timescopetoday" => PatternTimeScope.Today,
            "timescopeyesterday" => PatternTimeScope.Yesterday,
            "timescopecurrentweek" => PatternTimeScope.CurrentWeek,
            "timescopecurrentmonth" => PatternTimeScope.CurrentMonth,
            "timescopecurrentshift" => PatternTimeScope.CurrentShift,
            _ => PatternTimeScope.Unknown
        };
    }

    private static PatternMetric ParseMetric(string? metricKey)
    {
        return NormalizeKey(metricKey) switch
        {
            "scrapqty" => PatternMetric.ScrapQty,
            "scrapcost" => PatternMetric.ScrapCost,
            "producedqty" => PatternMetric.ProducedQty,
            "downtimeminutes" => PatternMetric.DownTimeMinutes,
            "downtimecost" => PatternMetric.DownTimeCost,
            "totalloss" => PatternMetric.TotalLoss,
            _ => PatternMetric.Unknown
        };
    }

    private static PatternDimension ParseDimension(string? dimensionKey)
    {
        return NormalizeKey(dimensionKey) switch
        {
            "press" or "prensa" => PatternDimension.Press,
            "mold" or "molde" => PatternDimension.Mold,
            "failure" or "falla" => PatternDimension.Failure,
            "department" or "departamento" => PatternDimension.Department,
            "partnumber" or "numerodeparte" or "numerosdeparte" => PatternDimension.PartNumber,
            _ => PatternDimension.Unknown
        };
    }

    private static PatternTimeScope ParseTimeScope(string? timeScopeKey)
    {
        return NormalizeKey(timeScopeKey) switch
        {
            "today" or "hoy" => PatternTimeScope.Today,
            "yesterday" or "ayer" => PatternTimeScope.Yesterday,
            "currentweek" or "thisweek" or "semanaactual" => PatternTimeScope.CurrentWeek,
            "currentmonth" or "thismonth" or "mesactual" => PatternTimeScope.CurrentMonth,
            "currentshift" or "turnoactual" => PatternTimeScope.CurrentShift,
            _ => PatternTimeScope.Unknown
        };
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var formD = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);

        foreach (var c in formD)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }

        return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }

    private static string NormalizeKey(string? value)
    {
        var normalized = NormalizeText(value);
        return normalized.Replace(" ", string.Empty, StringComparison.Ordinal);
    }

    private static PatternMatchResult TryBuiltInMatch(string question, string? domain)
    {
        var normalizedDomain = NormalizeKey(domain);
        if (string.IsNullOrWhiteSpace(normalizedDomain))
            return new PatternMatchResult();

        var normalizedQuestion = NormalizeText(question);
        if (string.IsNullOrWhiteSpace(normalizedQuestion))
            return new PatternMatchResult();

        var topN = ExtractTopN(question);
        var timeScope = ResolveBuiltInTimeScope(normalizedQuestion);

        var isScrapQuestion = normalizedQuestion.Contains("scrap", StringComparison.Ordinal);

        if (isScrapQuestion &&
            ContainsAny(normalizedQuestion, "numero de parte", "numeros de parte", "part number", "part numbers"))
        {
            return new PatternMatchResult
            {
                IsMatch = true,
                PatternKey = "top_scrap_by_partnumber",
                IntentName = "top_scrap_by_partnumber",
                TopN = topN > 0 ? topN : 5,
                Metric = PatternMetric.ScrapQty,
                Dimension = PatternDimension.PartNumber,
                DimensionValue = ExtractDimensionValue(question, PatternDimension.PartNumber),
                TimeScope = timeScope
            };
        }

        if (isScrapQuestion &&
            ContainsAny(normalizedQuestion, "prensa", "prensas", "press"))
        {
            return new PatternMatchResult
            {
                IsMatch = true,
                PatternKey = "top_scrap_by_press",
                IntentName = "top_scrap_by_press",
                TopN = topN > 0 ? topN : 5,
                Metric = PatternMetric.ScrapQty,
                Dimension = PatternDimension.Press,
                DimensionValue = ExtractDimensionValue(question, PatternDimension.Press),
                TimeScope = timeScope
            };
        }

        if (ContainsAny(normalizedQuestion, "produccion", "production", "producido") &&
            ContainsAny(normalizedQuestion, "total", "cuanto", "cuanta"))
        {
            return new PatternMatchResult
            {
                IsMatch = true,
                PatternKey = "total_production",
                IntentName = "total_production",
                Metric = PatternMetric.ProducedQty,
                Dimension = PatternDimension.Unknown,
                TimeScope = timeScope
            };
        }

        if (ContainsAny(normalizedQuestion, "downtime", "paro") &&
            ContainsAny(normalizedQuestion, "falla", "fallas", "failure"))
        {
            return new PatternMatchResult
            {
                IsMatch = true,
                PatternKey = "top_downtime_by_failure",
                IntentName = "top_downtime_by_failure",
                TopN = topN > 0 ? topN : 5,
                Metric = PatternMetric.DownTimeMinutes,
                Dimension = PatternDimension.Failure,
                TimeScope = timeScope
            };
        }

        if (ContainsAny(normalizedQuestion, "downtime", "paro") &&
            ContainsAny(normalizedQuestion, "departamento", "department", "area"))
        {
            return new PatternMatchResult
            {
                IsMatch = true,
                PatternKey = "downtime_by_department",
                IntentName = "downtime_by_department",
                TopN = topN > 0 ? topN : 5,
                Metric = PatternMetric.DownTimeMinutes,
                Dimension = PatternDimension.Department,
                TimeScope = timeScope
            };
        }

        if (isScrapQuestion &&
            ContainsAny(normalizedQuestion, "costo", "cost") &&
            ContainsAny(normalizedQuestion, "molde", "mold"))
        {
            return new PatternMatchResult
            {
                IsMatch = true,
                PatternKey = "top_scrap_cost_by_mold",
                IntentName = "top_scrap_cost_by_mold",
                TopN = topN > 0 ? topN : 5,
                Metric = PatternMetric.ScrapCost,
                Dimension = PatternDimension.Mold,
                TimeScope = timeScope
            };
        }

        return new PatternMatchResult();
    }

    private static PatternTimeScope ResolveBuiltInTimeScope(string normalizedQuestion)
    {
        if (ContainsAny(normalizedQuestion, "turno actual", "del turno", "turno en curso"))
            return PatternTimeScope.CurrentShift;

        if (ContainsAny(normalizedQuestion, "hoy", "dia de hoy", "today"))
            return PatternTimeScope.Today;

        if (ContainsAny(normalizedQuestion, "ayer", "yesterday"))
            return PatternTimeScope.Yesterday;

        if (ContainsAny(normalizedQuestion, "este mes", "mes actual", "del mes", "current month"))
            return PatternTimeScope.CurrentMonth;

        if (ContainsAny(normalizedQuestion, "esta semana", "semana actual", "de la semana", "current week"))
            return PatternTimeScope.CurrentWeek;

        return PatternTimeScope.CurrentShift;
    }

    private static string ExtractDimensionValue(string question, PatternDimension dimension)
    {
        if (string.IsNullOrWhiteSpace(question))
            return string.Empty;

        return dimension switch
        {
            PatternDimension.Press => ExtractTokenAfterKeyword(question, new[] { "prensa", "press" }),
            PatternDimension.PartNumber => ExtractTokenAfterKeyword(question, new[] { "numero de parte", "numeros de parte", "part number", "n/p", "np" }),
            PatternDimension.Mold => ExtractTokenAfterKeyword(question, new[] { "molde", "mold" }),
            PatternDimension.Failure => ExtractTokenAfterKeyword(question, new[] { "falla", "failure" }),
            PatternDimension.Department => ExtractTokenAfterKeyword(question, new[] { "departamento", "area", "department" }),
            _ => string.Empty
        };
    }

    private static string ExtractTokenAfterKeyword(string question, IReadOnlyList<string> keywords)
    {
        foreach (var keyword in keywords)
        {
            var pattern = $@"\b{Regex.Escape(keyword)}\b\s+(?<value>[A-Za-z0-9][A-Za-z0-9_./-]*)";
            var match = Regex.Match(question, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success)
                continue;

            var value = match.Groups["value"].Value.Trim();
            if (IsIgnorableDimensionValue(value))
                continue;

            return value;
        }

        return string.Empty;
    }

    private static bool IsIgnorableDimensionValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        return NormalizeKey(value) switch
        {
            "actual" or "actuales" or "del" or "de" or "con" or "que" or "mas" or "lleva" or "tiene" or "current" => true,
            _ => false
        };
    }

    private static bool ContainsAny(string normalizedQuestion, params string[] phrases)
    {
        foreach (var phrase in phrases)
        {
            var normalizedPhrase = NormalizeText(phrase);
            if (!string.IsNullOrWhiteSpace(normalizedPhrase) &&
                normalizedQuestion.Contains(normalizedPhrase, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private sealed record PatternEvaluation(
        QueryPattern Pattern,
        double Score,
        int RequiredMatchCount,
        int OptionalMatchCount,
        int ExactMatchCount,
        int TotalMatchCount,
        int MatchedGroupCount,
        bool RequiredSatisfied,
        bool HasIntentEvidence,
        bool IsStrongRouteMatch,
        int ExtractedTopN,
        PatternTimeScope ResolvedTimeScope);
}
