using VannaLight.Core.Models;

namespace VannaLight.Core.Abstractions;

public interface ISqlAlertQueryBuilder
{
    Task<SqlAlertQueryPlan> BuildAsync(SqlAlertRule rule, CancellationToken ct = default);
}
