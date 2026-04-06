using VannaLight.Core.Models;

namespace VannaLight.Core.Abstractions;

public interface IDomainPackProvider
{
    Task<DomainPackDefinition> GetDomainPackAsync(string domain, CancellationToken ct = default);
}
