using VannaLight.Core.Models;

namespace VannaLight.Core.Abstractions;

public interface IExecutionContextResolver
{
    Task<AskExecutionContext> ResolveAsync(
        string? tenantKey = null,
        string? domain = null,
        string? connectionName = null,
        CancellationToken ct = default);
}
