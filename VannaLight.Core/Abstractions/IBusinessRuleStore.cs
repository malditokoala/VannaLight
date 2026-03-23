using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VannaLight.Core.Models;

namespace VannaLight.Core.Abstractions;

public interface IBusinessRuleStore
{
    Task<IReadOnlyList<BusinessRule>> GetActiveRulesAsync(
        string sqlitePath,
        string domain,
        int maxRules,
        CancellationToken ct = default);

    Task<IReadOnlyList<BusinessRule>> GetAllRulesAsync(
        string sqlitePath,
        string domain,
        CancellationToken ct = default);

    Task<long> UpsertAsync(
        string sqlitePath,
        BusinessRule rule,
        CancellationToken ct = default);

    Task<bool> SetIsActiveAsync(
        string sqlitePath,
        long id,
        bool isActive,
        CancellationToken ct = default);
}