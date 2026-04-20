using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Infrastructure.Data;

public sealed class SqlAlertEvaluator(
    ISqlAlertQueryBuilder queryBuilder,
    IOperationalConnectionResolver operationalConnectionResolver,
    ILogger<SqlAlertEvaluator> logger) : ISqlAlertEvaluator
{
    public async Task<SqlAlertEvaluationOutcome> EvaluateAsync(SqlAlertRule rule, CancellationToken ct = default)
    {
        try
        {
            var plan = await queryBuilder.BuildAsync(rule, ct);
            var connectionString = await operationalConnectionResolver.ResolveConnectionStringAsync(rule.ConnectionName, ct);

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(ct);

            var observed = await conn.ExecuteScalarAsync<decimal?>(new CommandDefinition(
                plan.Sql,
                ToDynamicParameters(plan.Parameters),
                commandTimeout: 15,
                cancellationToken: ct));

            var observedValue = observed ?? 0m;
            var conditionMet = Compare(observedValue, rule.Threshold, rule.ComparisonOperator);
            var comparisonText = rule.ComparisonOperator switch
            {
                SqlAlertComparisonOperator.GreaterThan => "superó",
                SqlAlertComparisonOperator.GreaterThanOrEqual => "igualó o superó",
                SqlAlertComparisonOperator.LessThan => "quedó por debajo de",
                SqlAlertComparisonOperator.LessThanOrEqual => "quedó en o por debajo de",
                SqlAlertComparisonOperator.Equal => "igualó",
                SqlAlertComparisonOperator.NotEqual => "cambió respecto a",
                _ => "evaluó"
            };

            return new SqlAlertEvaluationOutcome
            {
                Success = true,
                ConditionMet = conditionMet,
                ObservedValue = observedValue,
                Message = conditionMet
                    ? $"{plan.MetricDisplayName} {comparisonText} el umbral configurado ({observedValue} vs {rule.Threshold})."
                    : $"{plan.MetricDisplayName} sigue dentro del rango esperado ({observedValue} vs {rule.Threshold}).",
                QueryPlan = plan
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fallo evaluando alerta SQL {RuleKey} ({DisplayName})", rule.RuleKey, rule.DisplayName);
            return new SqlAlertEvaluationOutcome
            {
                Success = false,
                ConditionMet = false,
                ErrorMessage = ex.Message,
                Message = $"No se pudo evaluar la alerta '{rule.DisplayName}'."
            };
        }
    }

    private static DynamicParameters ToDynamicParameters(IReadOnlyDictionary<string, object?> parameters)
    {
        var bag = new DynamicParameters();
        foreach (var item in parameters)
        {
            bag.Add(item.Key, item.Value);
        }
        return bag;
    }

    private static bool Compare(decimal observed, decimal threshold, SqlAlertComparisonOperator op)
        => op switch
        {
            SqlAlertComparisonOperator.GreaterThan => observed > threshold,
            SqlAlertComparisonOperator.GreaterThanOrEqual => observed >= threshold,
            SqlAlertComparisonOperator.LessThan => observed < threshold,
            SqlAlertComparisonOperator.LessThanOrEqual => observed <= threshold,
            SqlAlertComparisonOperator.Equal => observed == threshold,
            SqlAlertComparisonOperator.NotEqual => observed != threshold,
            _ => false
        };
}

