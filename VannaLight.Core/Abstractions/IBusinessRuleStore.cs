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
}