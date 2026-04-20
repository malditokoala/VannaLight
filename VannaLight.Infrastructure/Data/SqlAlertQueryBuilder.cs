using System.Text.RegularExpressions;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Infrastructure.Data;

public sealed class SqlAlertQueryBuilder(
    ISqlAlertMetricCatalog metricCatalog,
    IAllowedObjectStore allowedObjectStore,
    IMlTrainingProfileProvider mlTrainingProfileProvider) : ISqlAlertQueryBuilder
{
    private static readonly Regex SafeIdentifierRegex = new(@"^[\[\]\w\.]+$", RegexOptions.Compiled);

    public async Task<SqlAlertQueryPlan> BuildAsync(SqlAlertRule rule, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(rule);
        rule.ThrowIfInvalid();

        var catalog = await metricCatalog.GetCatalogAsync(rule.Domain, rule.ConnectionName, ct);
        var metric = catalog.Metrics.FirstOrDefault(x => string.Equals(x.Key, rule.MetricKey, StringComparison.OrdinalIgnoreCase));
        if (metric is null)
            throw new InvalidOperationException($"La metrica '{rule.MetricKey}' no existe en el catalogo del dominio '{rule.Domain}'.");

        var baseObject = NormalizeSafeIdentifier(metric.BaseObject, "BaseObject");
        var (schemaName, objectName) = ParseQualifiedObject(baseObject);
        var isAllowed = await allowedObjectStore.IsAllowedAsync(rule.Domain, schemaName, objectName, ct);
        if (!isAllowed)
            throw new InvalidOperationException($"El objeto '{baseObject}' no esta permitido para el dominio '{rule.Domain}'.");

        DimensionDefinition? dimension = null;
        if (!string.IsNullOrWhiteSpace(rule.DimensionKey))
        {
            dimension = catalog.Dimensions.FirstOrDefault(x => string.Equals(x.Key, rule.DimensionKey, StringComparison.OrdinalIgnoreCase));
            if (dimension is null)
                throw new InvalidOperationException($"La dimension '{rule.DimensionKey}' no existe en el dominio '{rule.Domain}'.");
            if (!metric.AllowedDimensions.Contains(rule.DimensionKey, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException($"La metrica '{metric.Key}' no soporta la dimension '{rule.DimensionKey}'.");
        }

        var metricExpression = NormalizeSafeIdentifier(metric.SqlExpression, "SqlExpression", allowFunctions: true);
        var dimensionExpression = dimension is null ? null : NormalizeSafeIdentifier(dimension.SqlExpression, "DimensionSqlExpression");

        var parameters = new Dictionary<string, object?>();
        var filters = new List<string>();

        filters.Add(await BuildTimeFilterAsync(rule, metric, parameters, ct));

        if (!string.IsNullOrWhiteSpace(dimensionExpression) && !string.IsNullOrWhiteSpace(rule.DimensionValue))
        {
            filters.Add($"{dimensionExpression} = @DimensionValue");
            parameters["DimensionValue"] = rule.DimensionValue!.Trim();
        }

        var whereClause = filters.Count > 0 ? $"WHERE {string.Join(" AND ", filters)}" : string.Empty;
        var sql = $@"
SELECT
    CAST(COALESCE({metricExpression}, 0) AS decimal(18,4)) AS ObservedValue
FROM {baseObject} src
{whereClause};".Trim();

        return new SqlAlertQueryPlan
        {
            Sql = sql,
            Parameters = parameters,
            Summary = BuildSummary(rule, metric, dimension),
            BaseObject = baseObject,
            MetricDisplayName = metric.DisplayName,
            DimensionDisplayName = dimension?.DisplayName
        };
    }

    private async Task<string> BuildTimeFilterAsync(SqlAlertRule rule, MetricDefinition metric, IDictionary<string, object?> parameters, CancellationToken ct)
    {
        var timeColumn = NormalizeSafeIdentifier(metric.TimeColumn, nameof(metric.TimeColumn));
        return rule.TimeScope switch
        {
            SqlAlertTimeScope.Today => $"CAST({timeColumn} AS date) = CAST(GETDATE() AS date)",
            SqlAlertTimeScope.Last24Hours => $"{timeColumn} >= DATEADD(hour, -24, GETDATE())",
            SqlAlertTimeScope.CurrentWeek => $"{timeColumn} >= DATEADD(day, -((DATEPART(weekday, GETDATE()) + @@DATEFIRST - 2) % 7), CAST(GETDATE() AS date))",
            SqlAlertTimeScope.CurrentMonth => $"{timeColumn} >= DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1)",
            SqlAlertTimeScope.CurrentShift => await BuildCurrentShiftFilterAsync(rule, parameters, ct),
            _ => throw new InvalidOperationException($"TimeScope no soportado: {rule.TimeScope}")
        };
    }

    private async Task<string> BuildCurrentShiftFilterAsync(SqlAlertRule rule, IDictionary<string, object?> parameters, CancellationToken ct)
    {
        var profile = await mlTrainingProfileProvider.GetActiveProfileAsync(ct);
        var shiftTable = NormalizeSafeIdentifier(profile.ShiftTableQualifiedName, "ShiftTableQualifiedName");
        var (schemaName, objectName) = ParseQualifiedObject(shiftTable);
        var isAllowed = await allowedObjectStore.IsAllowedAsync(rule.Domain, schemaName, objectName, ct);
        if (!isAllowed)
            throw new InvalidOperationException($"La tabla de turnos '{shiftTable}' no esta permitida para el dominio '{rule.Domain}'.");

        parameters["CurrentTicks"] = DateTime.Now.TimeOfDay.Ticks;

        return $@"
CAST(src.OperationDate AS date) = CAST(GETDATE() AS date)
AND src.ShiftId IN (
    SELECT Id
    FROM {shiftTable}
    WHERE disponibleProduccion = 1
      AND ((inicio <= fin AND @CurrentTicks BETWEEN inicio AND fin)
        OR (inicio > fin AND (@CurrentTicks >= inicio OR @CurrentTicks <= fin)))
)".Trim();
    }

    private static string BuildSummary(SqlAlertRule rule, MetricDefinition metric, DimensionDefinition? dimension)
    {
        var comparison = rule.ComparisonOperator switch
        {
            SqlAlertComparisonOperator.GreaterThan => ">",
            SqlAlertComparisonOperator.GreaterThanOrEqual => ">=",
            SqlAlertComparisonOperator.LessThan => "<",
            SqlAlertComparisonOperator.LessThanOrEqual => "<=",
            SqlAlertComparisonOperator.Equal => "=",
            SqlAlertComparisonOperator.NotEqual => "!=",
            _ => "?"
        };

        var scope = rule.TimeScope switch
        {
            SqlAlertTimeScope.Today => "hoy",
            SqlAlertTimeScope.Last24Hours => "ultimas 24h",
            SqlAlertTimeScope.CurrentShift => "turno actual",
            SqlAlertTimeScope.CurrentWeek => "semana actual",
            SqlAlertTimeScope.CurrentMonth => "mes actual",
            _ => rule.TimeScope.ToString()
        };

        var dimensionText = dimension is null || string.IsNullOrWhiteSpace(rule.DimensionValue)
            ? string.Empty
            : $" · {dimension.DisplayName} = {rule.DimensionValue}";

        return $"{metric.DisplayName} {comparison} {rule.Threshold}{dimensionText} · {scope}";
    }

    private static string NormalizeSafeIdentifier(string value, string fieldName, bool allowFunctions = false)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new InvalidOperationException($"{fieldName} esta vacio en la definicion de alerta SQL.");

        if (allowFunctions)
        {
            if (trimmed.Contains(';', StringComparison.Ordinal) || trimmed.Contains("--", StringComparison.Ordinal) || trimmed.Contains("/*", StringComparison.Ordinal))
                throw new InvalidOperationException($"{fieldName} contiene tokens inseguros.");
            return trimmed;
        }

        if (!SafeIdentifierRegex.IsMatch(trimmed))
            throw new InvalidOperationException($"{fieldName} contiene un identificador no permitido: {trimmed}");

        return trimmed;
    }

    private static (string SchemaName, string ObjectName) ParseQualifiedObject(string qualifiedName)
    {
        var normalized = qualifiedName.Trim().Replace("[", string.Empty, StringComparison.Ordinal).Replace("]", string.Empty, StringComparison.Ordinal);
        var parts = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
            return ("dbo", parts[0]);
        return (parts[0], parts[^1]);
    }
}
