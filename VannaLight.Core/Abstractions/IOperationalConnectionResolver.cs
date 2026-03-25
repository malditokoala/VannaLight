namespace VannaLight.Core.Abstractions;

public interface IOperationalConnectionResolver
{
    Task<string> ResolveOperationalConnectionStringAsync(CancellationToken ct = default);
}