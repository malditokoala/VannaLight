using VannaLight.Core.Settings;

namespace VannaLight.Api.Services.Predictions;

internal static class MlTrainingSqlBuilder
{
    public static string BuildTrainingDatasetSql(MlTrainingProfile profile)
    {
        if (profile.UsesCustomSql)
        {
            var sourceSql = profile.TrainingSqlNormalized;
            if (string.IsNullOrWhiteSpace(sourceSql))
                throw new InvalidOperationException("ML custom SQL source is empty.");

            return sourceSql;
        }

        return $@"
            WITH TurnosActivos AS (
                SELECT Id AS ShiftId, nombre AS ShiftName, inicio AS TicksInicio, fin AS TicksFin
                FROM {profile.ShiftTableQualifiedName}
                WHERE disponibleProduccion = 1
            ),
            BaseTimeline AS (
                SELECT LTRIM(RTRIM(p.PartNumber)) AS PartNumber, p.OperationDate, p.ShiftId
                FROM {profile.ProductionViewQualifiedName} p
                JOIN TurnosActivos t ON p.ShiftId = t.ShiftId
                WHERE p.OperationDate IS NOT NULL AND p.PartNumber IS NOT NULL
                UNION
                SELECT LTRIM(RTRIM(s.PartNumber)) AS PartNumber, s.OperationDate, s.ShiftId
                FROM {profile.ScrapViewQualifiedName} s
                JOIN TurnosActivos t ON s.ShiftId = t.ShiftId
                WHERE s.OperationDate IS NOT NULL AND s.PartNumber IS NOT NULL
            ),
            ScrapShift AS (
                SELECT LTRIM(RTRIM(s.PartNumber)) AS PartNumber, s.OperationDate, s.ShiftId, 
                       SUM(CAST(ISNULL(s.ScrapQty, 0) AS float)) AS ScrapQty
                FROM {profile.ScrapViewQualifiedName} s
                JOIN TurnosActivos t ON s.ShiftId = t.ShiftId
                WHERE s.OperationDate IS NOT NULL AND s.PartNumber IS NOT NULL
                GROUP BY LTRIM(RTRIM(s.PartNumber)), s.OperationDate, s.ShiftId
            )
                SELECT 
                b.PartNumber AS SeriesKey,
                b.OperationDate AS ObservedOn,
                b.ShiftId AS BucketKey,
                t.ShiftName AS BucketLabel,
                t.TicksInicio AS BucketStartTick,
                t.TicksFin AS BucketEndTick,
                CAST(ISNULL(s.ScrapQty, 0) AS float) AS TargetValue
            FROM BaseTimeline b
            JOIN TurnosActivos t ON b.ShiftId = t.ShiftId
            LEFT JOIN ScrapShift s ON b.PartNumber = s.PartNumber AND b.OperationDate = s.OperationDate AND b.ShiftId = s.ShiftId
            ORDER BY b.PartNumber, b.OperationDate, t.TicksInicio;";
    }

    public static string BuildActiveShiftsSql(MlTrainingProfile profile)
    {
        if (profile.UsesCustomSql)
        {
            return $@"
                WITH SourceData AS (
                    {profile.TrainingSqlNormalized}
                )
                SELECT DISTINCT
                    BucketKey,
                    BucketLabel,
                    BucketStartTick,
                    BucketEndTick
                FROM SourceData
                WHERE BucketKey IS NOT NULL
                ORDER BY BucketStartTick;";
        }

        return $@"
            SELECT
                Id AS BucketKey,
                nombre AS BucketLabel,
                inicio AS BucketStartTick,
                fin AS BucketEndTick
            FROM {profile.ShiftTableQualifiedName}
            WHERE disponibleProduccion = 1
            ORDER BY inicio;";
    }

    public static string BuildShiftHistorySql(MlTrainingProfile profile)
    {
        if (profile.UsesCustomSql)
        {
            return $@"
                WITH SourceData AS (
                    {profile.TrainingSqlNormalized}
                )
                SELECT
                    LTRIM(RTRIM(SeriesKey)) AS SeriesKey,
                    ObservedOn,
                    BucketKey,
                    CAST(ISNULL(TargetValue, 0) AS float) AS TargetValue
                FROM SourceData
                WHERE ObservedOn IS NOT NULL
                  AND LTRIM(RTRIM(SeriesKey)) = @SeriesKey
                ORDER BY ObservedOn, BucketStartTick;";
        }

        return $@"
            WITH TurnosActivos AS (
                SELECT Id AS BucketKey, inicio AS BucketStartTick FROM {profile.ShiftTableQualifiedName} WHERE disponibleProduccion = 1
            ),
            BaseTimeline AS (
                SELECT LTRIM(RTRIM(PartNumber)) AS SeriesKey, OperationDate AS ObservedOn, ShiftId AS BucketKey
                FROM {profile.ProductionViewQualifiedName} WHERE OperationDate IS NOT NULL AND LTRIM(RTRIM(PartNumber)) = @SeriesKey
                UNION
                SELECT LTRIM(RTRIM(PartNumber)) AS SeriesKey, OperationDate AS ObservedOn, ShiftId AS BucketKey
                FROM {profile.ScrapViewQualifiedName} WHERE OperationDate IS NOT NULL AND LTRIM(RTRIM(PartNumber)) = @SeriesKey
            ),
            ScrapShift AS (
                SELECT LTRIM(RTRIM(PartNumber)) AS SeriesKey, OperationDate AS ObservedOn, ShiftId AS BucketKey, SUM(CAST(ISNULL(ScrapQty, 0) AS float)) AS TargetValue
                FROM {profile.ScrapViewQualifiedName} WHERE OperationDate IS NOT NULL AND LTRIM(RTRIM(PartNumber)) = @SeriesKey
                GROUP BY LTRIM(RTRIM(PartNumber)), OperationDate, ShiftId
            )
            SELECT b.SeriesKey, b.ObservedOn, b.BucketKey, CAST(ISNULL(s.TargetValue, 0) AS float) AS TargetValue
            FROM BaseTimeline b
            JOIN TurnosActivos t ON b.BucketKey = t.BucketKey
            LEFT JOIN ScrapShift s ON b.SeriesKey = s.SeriesKey AND b.ObservedOn = s.ObservedOn AND b.BucketKey = s.BucketKey
            ORDER BY b.ObservedOn, t.BucketStartTick;";
    }
}
