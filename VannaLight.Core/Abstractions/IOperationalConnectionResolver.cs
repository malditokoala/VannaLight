namespace VannaLight.Core.Abstractions;

public interface IOperationalConnectionResolver
{
    Task<string> ResolveConnectionStringAsync(string connectionName, CancellationToken ct = default);
    Task<string> ResolveOperationalConnectionStringAsync(CancellationToken ct = default);
}
