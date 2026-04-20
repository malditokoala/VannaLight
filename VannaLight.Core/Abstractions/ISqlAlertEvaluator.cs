using VannaLight.Core.Models;

namespace VannaLight.Core.Abstractions;

public interface ISqlAlertEvaluator
{
    Task<SqlAlertEvaluationOutcome> EvaluateAsync(SqlAlertRule rule, CancellationToken ct = default);
}
