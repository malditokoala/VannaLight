using Microsoft.Extensions.Configuration;
using VannaLight.Core.Abstractions;

namespace VannaLight.Infrastructure.Security;

public class CompositeSecretResolver : ISecretResolver
{
    private readonly IConfiguration _configuration;

    public CompositeSecretResolver(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<string?> ResolveAsync(string secretRef, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(secretRef))
            return Task.FromResult<string?>(null);

        if (secretRef.StartsWith("env:", StringComparison.OrdinalIgnoreCase))
        {
            var envKey = secretRef.Substring(4);
            return Task.FromResult(Environment.GetEnvironmentVariable(envKey));
        }

        if (secretRef.StartsWith("config:", StringComparison.OrdinalIgnoreCase))
        {
            var configKey = secretRef.Substring(7);
            return Task.FromResult(_configuration[configKey]);
        }

        return Task.FromResult<string?>(null);
    }
}