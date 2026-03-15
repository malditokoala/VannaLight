using Dapper;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Infrastructure.Data;

public class SqliteBusinessRuleStore : IBusinessRuleStore
{
    public async Task<IReadOnlyList<BusinessRule>> GetActiveRulesAsync(
        string sqlitePath,
        string domain,
        int maxRules,
        CancellationToken ct = default)
    {
        using var conn = new SqliteConnection($"Data Source={sqlitePath};");
        await conn.OpenAsync(ct);

        const string sql = @"
SELECT
    Id,
    Domain,
    RuleKey,
    RuleText,
    Priority,
    IsActive
FROM BusinessRules
WHERE Domain = @Domain
  AND IsActive = 1
ORDER BY Priority ASC
LIMIT @MaxRules;";

        var rows = await conn.QueryAsync<BusinessRule>(sql, new
        {
            Domain = domain,
            MaxRules = maxRules
        });

        return rows.AsList();
    }
}