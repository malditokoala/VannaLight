using System.Text;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Infrastructure.Retrieval;

public class TemplateSqlBuilder : ITemplateSqlBuilder
{
    public string BuildSql(PatternMatchResult match)
    {
        if (!match.IsMatch)
            return string.Empty;

        return match.PatternKey switch
        {
            "top_scrap_by_press" => BuildTopScrapByPress(match),
            "total_production" => BuildTotalProduction(match),
            "top_downtime_by_failure" => BuildTopDowntimeByFailure(match),
            "downtime_by_department" => BuildDowntimeByDepartment(match),
            "top_scrap_cost_by_mold" => BuildTopScrapCostByMold(match),
            _ => string.Empty
        };
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
}
