using System;
using System.Text;
using System.Text.RegularExpressions;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Infrastructure.Retrieval;

public class TemplateSqlBuilder : ITemplateSqlBuilder
{
    private static readonly Regex PlaceholderRegex = new(@"\{(?<token>[^{}]+)\}", RegexOptions.Compiled);
    private static readonly Regex FromAliasRegex = new(@"\bFROM\s+(?<source>[\[\]\w\.]+)\s+(?:AS\s+)?(?<alias>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public bool Supports(string patternKey)
    {
        if (string.IsNullOrWhiteSpace(patternKey))
            return false;

        return patternKey switch
        {
            "top_scrap_by_press" => true,
            "total_production" => true,
            "top_downtime_by_failure" => true,
            "top_downtime_by_press" => true,
            "downtime_by_department" => true,
            "top_scrap_cost_by_mold" => true,
            _ => false
        };
    }

    public string BuildSql(PatternMatchResult match)
    {
        if (!match.IsMatch)
            return string.Empty;

        var templateSql = TryBuildFromTemplate(match);
        if (!string.IsNullOrWhiteSpace(templateSql))
            return templateSql;

        if (!Supports(match.PatternKey))
            return string.Empty;

        return match.PatternKey switch
        {
            "top_scrap_by_press" => BuildTopScrapByPress(match),
            "total_production" => BuildTotalProduction(match),
            "top_downtime_by_failure" => BuildTopDowntimeByFailure(match),
            "top_downtime_by_press" => BuildTopDowntimeByPress(match),
            "downtime_by_department" => BuildDowntimeByDepartment(match),
            "top_scrap_cost_by_mold" => BuildTopScrapCostByMold(match),
            _ => string.Empty
        };
    }

    private static string TryBuildFromTemplate(PatternMatchResult match)
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

        return HasUnresolvedPlaceholders(rendered)
            ? string.Empty
            : rendered.Trim();
    }

    private static string BuildTopScrapByPress(PatternMatchResult match)
    {
        var top = match.TopN > 0 ? match.TopN : 5;

        return $@"
SELECT TOP ({top})
    s.PressId,
    s.PressName,
    SUM(ISNULL(s.ScrapQty, 0)) AS TotalScrapQty
FROM dbo.vw_KpiScrap_v1 s
WHERE {BuildTimeFilter("s", match.TimeScope, includeIsOpenForDowntime: false)}
GROUP BY s.PressId, s.PressName
ORDER BY TotalScrapQty DESC, s.PressName;".Trim();
    }

    private static string BuildTotalProduction(PatternMatchResult match)
    {
        return $@"
SELECT
    SUM(ISNULL(p.ProducedQty, 0)) AS TotalProducedQty
FROM dbo.vw_KpiProduction_v1 p
WHERE {BuildTimeFilter("p", match.TimeScope, includeIsOpenForDowntime: false)};".Trim();
    }

    private static string BuildTopDowntimeByFailure(PatternMatchResult match)
    {
        var top = match.TopN > 0 ? match.TopN : 5;

        return $@"
SELECT TOP ({top})
    d.FailureName,
    SUM(ISNULL(d.DownTimeMinutes, 0)) AS TotalDownTimeMinutes,
    SUM(ISNULL(d.DownTimeCost, 0)) AS TotalDownTimeCost
FROM dbo.vw_KpiDownTime_v1 d
WHERE {BuildTimeFilter("d", match.TimeScope, includeIsOpenForDowntime: true)}
GROUP BY d.FailureName
ORDER BY TotalDownTimeMinutes DESC, d.FailureName;".Trim();
    }

    private static string BuildTopDowntimeByPress(PatternMatchResult match)
    {
        var top = match.TopN > 0 ? match.TopN : 5;

        return $@"
SELECT TOP ({top})
    d.PressId,
    d.PressName,
    SUM(ISNULL(d.DownTimeMinutes, 0)) AS TotalDownTimeMinutes,
    SUM(ISNULL(d.DownTimeCost, 0)) AS TotalDownTimeCost
FROM dbo.vw_KpiDownTime_v1 d
WHERE {BuildTimeFilter("d", match.TimeScope, includeIsOpenForDowntime: true)}
GROUP BY d.PressId, d.PressName
ORDER BY TotalDownTimeMinutes DESC, d.PressName;".Trim();
    }

    private static string BuildDowntimeByDepartment(PatternMatchResult match)
    {
        var selectTop = match.TopN > 0 ? $"TOP ({match.TopN})" : string.Empty;

        return $@"
SELECT {selectTop}
    d.DepartmentName,
    SUM(ISNULL(d.DownTimeMinutes, 0)) AS TotalDownTimeMinutes,
    SUM(ISNULL(d.DownTimeCost, 0)) AS TotalDownTimeCost
FROM dbo.vw_KpiDownTime_v1 d
WHERE {BuildTimeFilter("d", match.TimeScope, includeIsOpenForDowntime: true)}
GROUP BY d.DepartmentName
ORDER BY TotalDownTimeMinutes DESC, d.DepartmentName;".Trim();
    }

    private static string BuildTopScrapCostByMold(PatternMatchResult match)
    {
        var top = match.TopN > 0 ? match.TopN : 5;

        return $@"
SELECT TOP ({top})
    s.MoldId,
    s.MoldName,
    SUM(ISNULL(s.ScrapCost, 0)) AS TotalScrapCost
FROM dbo.vw_KpiScrap_v1 s
WHERE {BuildTimeFilter("s", match.TimeScope, includeIsOpenForDowntime: false)}
GROUP BY s.MoldId, s.MoldName
ORDER BY TotalScrapCost DESC, s.MoldName;".Trim();
    }

    private static string BuildTimeFilter(string alias, PatternTimeScope scope, bool includeIsOpenForDowntime)
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

    private static string ResolveViewForAlias(string alias)
    {
        return alias switch
        {
            "p" => "dbo.vw_KpiProduction_v1",
            "s" => "dbo.vw_KpiScrap_v1",
            "d" => "dbo.vw_KpiDownTime_v1",
            _ => "dbo.vw_KpiProduction_v1"
        };
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

    private static string? ResolveViewName(PatternMatchResult match)
    {
        return match.Metric switch
        {
            PatternMetric.ScrapQty or PatternMetric.ScrapCost => "dbo.vw_KpiScrap_v1",
            PatternMetric.ProducedQty => "dbo.vw_KpiProduction_v1",
            PatternMetric.DownTimeMinutes or PatternMetric.DownTimeCost or PatternMetric.TotalLoss => "dbo.vw_KpiDownTime_v1",
            _ => null
        };
    }

    private static bool HasUnresolvedPlaceholders(string sql)
    {
        return PlaceholderRegex.IsMatch(sql);
    }
}
