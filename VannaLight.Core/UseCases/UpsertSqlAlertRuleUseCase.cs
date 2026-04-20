using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Core.UseCases;

public sealed class UpsertSqlAlertRuleUseCase(ISqlAlertRuleStore ruleStore)
{
    public async Task<long> ExecuteAsync(SqlAlertRule rule, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(rule);

        if (string.IsNullOrWhiteSpace(rule.RuleKey))
        {
            rule.RuleKey = $"sql-alert:{Guid.NewGuid():N}";
        }

        rule.ThrowIfInvalid();
        return await ruleStore.UpsertAsync(rule, ct);
    }
}
