namespace VannaLight.Core.Abstractions;

public interface ISecretResolver
{
    Task<string?> ResolveAsync(string secretRef, CancellationToken ct = default);
}