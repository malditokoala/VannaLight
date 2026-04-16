using System;
using System.Text;
using System.Text.RegularExpressions;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;
using VannaLight.Core.Settings;

namespace VannaLight.Infrastructure.Retrieval;

public class TemplateSqlBuilder : ITemplateSqlBuilder
{
    private static readonly Regex PlaceholderRegex = new(@"\{(?<token>[^{}]+)\}", RegexOptions.Compiled);
    private static readonly Regex FromAliasRegex = new(@"\bFROM\s+(?<source>[\[\]\w\.]+)\s+(?:AS\s+)?(?<alias>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly KpiViewOptions _kpiViews;

    public TemplateSqlBuilder(KpiViewOptions kpiViews)
    {
        _kpiViews = kpiViews;
    }

    public string BuildSql(PatternMatchResult match)
    {
        if (!match.IsMatch)
            return string.Empty;

        var templateSql = TryBuildFromTemplate(match);
        if (!string.IsNullOrWhiteSpace(templateSql))
            return templateSql;

        var genericSql = TryBuildGenericFallback(match);
        if (!string.IsNullOrWhiteSpace(genericSql))
            return genericSql;

        return string.Empty;
    }

    private string TryBuildFromTemplate(PatternMatchResult match)
    {
        if (string.IsNullOrWhiteSpace(match.SqlTemplate))
            return string.Empty;

        var rendered = match.SqlTemplate.Trim();
        rendered = ReplaceSimpleToken(rendered, "topn", (match.TopN > 0 ? match.TopN : 5).ToString());
        rendered = ReplaceSimpleToken(rendered, "patternkey", match.PatternKey);
        rendered = ReplaceSimpleToken(rendered, "intentname", match.IntentName);

        var alias = ResolveAlias(rendered);
        rendered = ReplaceToken(rendered, "timefilter", token =>
        {
            var explicitAlias = TryReadTokenArgument(token);
            var filterAlias = !string.IsNullOrWhiteSpace(explicitAlias) ? explicitAlias! : alias;
            if (string.IsNullOrWhiteSpace(filterAlias))
                return null;

            return BuildTimeFilter(filterAlias, match.TimeScope, ShouldIncludeIsOpenForDowntime(match));
        });

        rendered = ReplaceToken(rendered, "viewname", _ => ResolveViewName(match));
        rendered = ReplaceToken(rendered, "sourceview", _ => ResolveViewName(match));
        rendered = ReplaceToken(rendered, "dimensionprojection", token =>
        {
            var explicitAlias = TryReadTokenArgument(token);
            var projectionAlias = !string.IsNullOrWhiteSpace(explicitAlias) ? explicitAlias! : alias;
            if (string.IsNullOrWhiteSpace(projectionAlias))
                return null;

            return BuildDimensionProjection(projectionAlias, match.Dimension);
        });
        rendered = ReplaceToken(rendered, "dimensionfilter", token =>
        {
            var explicitAlias = TryReadTokenArgument(token);
            var filterAlias = !string.IsNullOrWhiteSpace(explicitAlias) ? explicitAlias! : alias;
            if (string.IsNullOrWhiteSpace(filterAlias))
                return string.Empty;

            return BuildDimensionFilter(filterAlias, match.Dimension);
        });
        rendered = ReplaceToken(rendered, "dimensiongroupby", token =>
        {
            var explicitAlias = TryReadTokenArgument(token);
            var groupAlias = !string.IsNullOrWhiteSpace(explicitAlias) ? explicitAlias! : alias;
            if (string.IsNullOrWhiteSpace(groupAlias))
                return null;

            return BuildDimensionGroupBy(groupAlias, match.Dimension);
        });
        rendered = ReplaceToken(rendered, "dimensionorderby", _ => BuildDimensionOrderBy(match.Dimension));
        rendered = ReplaceToken(rendered, "metricprojection", token =>
        {
            var explicitAlias = TryReadTokenArgument(token);
            var metricAlias = !string.IsNullOrWhiteSpace(explicitAlias) ? explicitAlias! : alias;
            if (string.IsNullOrWhiteSpace(metricAlias))
                return null;

            return BuildMetricProjection(metricAlias, match.Metric);
        });
        rendered = ReplaceToken(rendered, "metricorderby", _ => BuildMetricOrderBy(match.Metric));

        return HasUnresolvedPlaceholders(rendered)
            ? string.Empty
            : rendered.Trim();
    }

    private string TryBuildGenericGroupedTop(PatternMatchResult match)
    {
        var viewName = ResolveViewName(match);
        var dimensionProjection = BuildDimensionProjection("x", match.Dimension);
        var dimensionGroupBy = BuildDimensionGroupBy("x", match.Dimension);
        var dimensionOrderBy = BuildDimensionOrderBy(match.Dimension);
        var metricProjection = BuildMetricProjection("x", match.Metric);
        var metricOrderBy = BuildMetricOrderBy(match.Metric);

        if (string.IsNullOrWhiteSpace(viewName) ||
            string.IsNullOrWhiteSpace(dimensionProjection) ||
            string.IsNullOrWhiteSpace(dimensionGroupBy) ||
            string.IsNullOrWhiteSpace(dimensionOrderBy) ||
            string.IsNullOrWhiteSpace(metricProjection) ||
            string.IsNullOrWhiteSpace(metricOrderBy))
        {
            return string.Empty;
        }

        var top = match.TopN > 0 ? match.TopN : 5;
        var timeFilter = BuildTimeFilter("x", match.TimeScope, ShouldIncludeIsOpenForDowntime(match));
        var dimensionFilter = BuildDimensionFilter("x", match.Dimension);

        return $@"
SELECT TOP ({top})
    {dimensionProjection},
    {metricProjection}
FROM {viewName} x
WHERE {timeFilter}{dimensionFilter}
GROUP BY {dimensionGroupBy}
ORDER BY {metricOrderBy}, {dimensionOrderBy};".Trim();
    }

    private string TryBuildGenericTotal(PatternMatchResult match)
    {
        if (match.Dimension != PatternDimension.Unknown)
            return string.Empty;

        var viewName = ResolveViewName(match);
        var metricProjection = BuildMetricProjection("x", match.Metric);
        if (string.IsNullOrWhiteSpace(viewName) || string.IsNullOrWhiteSpace(metricProjection))
            return string.Empty;

        return $@"
SELECT
    {metricProjection}
FROM {viewName} x
WHERE {BuildTimeFilter("x", match.TimeScope, ShouldIncludeIsOpenForDowntime(match))};".Trim();
    }


    private string BuildTimeFilter(string alias, PatternTimeScope scope, bool includeIsOpenForDowntime)
    {
        var sb = new StringBuilder();

        switch (scope)
        {
            case PatternTimeScope.Today:
                sb.Append($"CAST({alias}.OperationDate AS date) = CAST(GETDATE() AS date)");
                break;

            case PatternTimeScope.Yesterday:
                sb.Append($"CAST({alias}.OperationDate AS date) = DATEADD(DAY, -1, CAST(GETDATE() AS date))");
                break;

            case PatternTimeScope.CurrentMonth:
                sb.Append($"{alias}.YearMonth = CONVERT(char(7), GETDATE(), 120)");
                break;

            case PatternTimeScope.CurrentShift:
                sb.Append($@"CAST({alias}.OperationDate AS date) = CAST(GETDATE() AS date)
  AND {alias}.ShiftId = (
      SELECT MAX(x.ShiftId)
      FROM {ResolveViewForAlias(alias)} x
      WHERE CAST(x.OperationDate AS date) = CAST(GETDATE() AS date)
  )");
                break;

            case PatternTimeScope.CurrentWeek:
            case PatternTimeScope.Unknown:
            default:
                sb.Append($"{alias}.YearNumber = YEAR(GETDATE()) AND {alias}.WeekOfYear = DATEPART(ISO_WEEK, GETDATE())");
                break;
        }

        if (includeIsOpenForDowntime)
        {
            sb.Append($"\n  AND {alias}.IsOpen = 0");
        }

        return sb.ToString();
    }

    private string ResolveViewForAlias(string alias)
    {
        return _kpiViews.ResolveByAlias(alias);
    }

    private static string ReplaceSimpleToken(string sql, string tokenName, string value)
    {
        return ReplaceToken(sql, tokenName, _ => value);
    }

    private static string ReplaceToken(string sql, string tokenName, Func<string, string?> resolver)
    {
        return PlaceholderRegex.Replace(sql, match =>
        {
            var token = match.Groups["token"].Value.Trim();
            var normalizedToken = NormalizeTokenName(token);
            if (!string.Equals(normalizedToken, tokenName, StringComparison.OrdinalIgnoreCase))
                return match.Value;

            var replacement = resolver(token);
            return string.IsNullOrWhiteSpace(replacement)
                ? match.Value
                : replacement;
        });
    }

    private static string NormalizeTokenName(string token)
    {
        var separatorIndex = token.IndexOf(':');
        if (separatorIndex >= 0)
            token = token[..separatorIndex];

        return token
            .Trim()
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);
    }

    private static string? TryReadTokenArgument(string token)
    {
        var separatorIndex = token.IndexOf(':');
        if (separatorIndex < 0 || separatorIndex == token.Length - 1)
            return null;

        var value = token[(separatorIndex + 1)..].Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? ResolveAlias(string sql)
    {
        var match = FromAliasRegex.Match(sql);
        if (!match.Success)
            return null;

        var alias = match.Groups["alias"].Value.Trim();
        return string.IsNullOrWhiteSpace(alias) ? null : alias;
    }

    private static bool ShouldIncludeIsOpenForDowntime(PatternMatchResult match)
    {
        return match.Metric is PatternMetric.DownTimeMinutes or PatternMetric.DownTimeCost or PatternMetric.TotalLoss;
    }

    private static string? BuildDimensionProjection(string alias, PatternDimension dimension)
    {
        return dimension switch
        {
            PatternDimension.Press =>
                $@"COALESCE(NULLIF(LTRIM(RTRIM({alias}.PressName)), ''), CONCAT('Prensa ', CAST({alias}.PressId AS varchar(32)))) AS Press,
    {alias}.PressId AS PressId",
            PatternDimension.PartNumber =>
                $"LTRIM(RTRIM({alias}.PartNumber)) AS PartNumber",
            PatternDimension.Mold =>
                $@"COALESCE(NULLIF(LTRIM(RTRIM({alias}.MoldName)), ''), CONCAT('Molde ', CAST({alias}.MoldId AS varchar(32)))) AS Mold,
    {alias}.MoldId AS MoldId",
            PatternDimension.Failure =>
                $@"COALESCE(NULLIF(LTRIM(RTRIM({alias}.FailureName)), ''), CONCAT('Falla ', CAST({alias}.FailureId AS varchar(32)))) AS Failure,
    {alias}.FailureId AS FailureId",
            PatternDimension.Department =>
                $@"{alias}.DepartmentId AS DepartmentId,
    {alias}.DepartmentName AS Department",
            _ => null
        };
    }

    private static string BuildDimensionFilter(string alias, PatternDimension dimension)
    {
        return dimension switch
        {
            PatternDimension.PartNumber => $"\n  AND NULLIF(LTRIM(RTRIM({alias}.PartNumber)), '') IS NOT NULL",
            PatternDimension.Mold => $"\n  AND ({alias}.MoldId IS NOT NULL OR NULLIF(LTRIM(RTRIM({alias}.MoldName)), '') IS NOT NULL)",
            PatternDimension.Failure => $"\n  AND ({alias}.FailureId IS NOT NULL OR NULLIF(LTRIM(RTRIM({alias}.FailureName)), '') IS NOT NULL)",
            PatternDimension.Department => $"\n  AND ({alias}.DepartmentId IS NOT NULL OR NULLIF(LTRIM(RTRIM({alias}.DepartmentName)), '') IS NOT NULL)",
            _ => string.Empty
        };
    }

    private string TryBuildGenericFallback(PatternMatchResult match)
    {
        if (match.Dimension != PatternDimension.Unknown)
            return TryBuildGenericGroupedTop(match);

        if (match.Metric != PatternMetric.Unknown)
            return TryBuildGenericTotal(match);

        return string.Empty;
    }

    private static string? BuildDimensionGroupBy(string alias, PatternDimension dimension)
    {
        return dimension switch
        {
            PatternDimension.Press => $"{alias}.PressId, {alias}.PressName",
            PatternDimension.PartNumber => $"LTRIM(RTRIM({alias}.PartNumber))",
            PatternDimension.Mold => $"{alias}.MoldId, {alias}.MoldName",
            PatternDimension.Failure => $"{alias}.FailureId, {alias}.FailureName",
            PatternDimension.Department => $"{alias}.DepartmentId, {alias}.DepartmentName",
            _ => null
        };
    }

    private static string? BuildDimensionOrderBy(PatternDimension dimension)
    {
        return dimension switch
        {
            PatternDimension.Press => "Press",
            PatternDimension.PartNumber => "PartNumber",
            PatternDimension.Mold => "Mold",
            PatternDimension.Failure => "Failure",
            PatternDimension.Department => "Department",
            _ => null
        };
    }

    private static string? BuildMetricProjection(string alias, PatternMetric metric)
    {
        return metric switch
        {
            PatternMetric.ScrapQty => $"SUM(ISNULL({alias}.ScrapQty, 0)) AS TotalScrapQty",
            PatternMetric.ScrapCost => $"SUM(ISNULL({alias}.ScrapCost, 0)) AS TotalScrapCost",
            PatternMetric.ProducedQty => $"SUM(ISNULL({alias}.ProducedQty, 0)) AS TotalProducedQty",
            PatternMetric.DownTimeMinutes => $@"SUM(ISNULL({alias}.DownTimeMinutes, 0)) AS TotalDownTimeMinutes,
    SUM(ISNULL({alias}.DownTimeCost, 0)) AS TotalDownTimeCost",
            PatternMetric.DownTimeCost => $@"SUM(ISNULL({alias}.DownTimeCost, 0)) AS TotalDownTimeCost,
    SUM(ISNULL({alias}.DownTimeMinutes, 0)) AS TotalDownTimeMinutes",
            PatternMetric.TotalLoss => $"SUM(ISNULL({alias}.TotalLoss, 0)) AS TotalLoss",
            _ => null
        };
    }

    private static string? BuildMetricOrderBy(PatternMetric metric)
    {
        return metric switch
        {
            PatternMetric.ScrapQty => "TotalScrapQty DESC",
            PatternMetric.ScrapCost => "TotalScrapCost DESC",
            PatternMetric.ProducedQty => "TotalProducedQty DESC",
            PatternMetric.DownTimeMinutes => "TotalDownTimeMinutes DESC",
            PatternMetric.DownTimeCost => "TotalDownTimeCost DESC",
            PatternMetric.TotalLoss => "TotalLoss DESC",
            _ => null
        };
    }

    private string? ResolveViewName(PatternMatchResult match)
    {
        return _kpiViews.ResolveByMetric(match.Metric);
    }

    private static bool HasUnresolvedPlaceholders(string sql)
    {
        return PlaceholderRegex.IsMatch(sql);
    }
}
