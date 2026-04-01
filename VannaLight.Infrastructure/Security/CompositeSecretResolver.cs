using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using VannaLight.Core.Abstractions;

namespace VannaLight.Infrastructure.Security;

public class CompositeSecretResolver : ISecretResolver
{
    private readonly IConfiguration _configuration;
    private readonly IAppSecretStore _appSecretStore;
    private readonly IDataProtector _protector;

    public CompositeSecretResolver(
        IConfiguration configuration,
        IAppSecretStore appSecretStore,
        IDataProtectionProvider dataProtectionProvider)
    {
        _configuration = configuration;
        _appSecretStore = appSecretStore;
        _protector = dataProtectionProvider.CreateProtector(AppSecretProtection.Purpose);
    }

    public async Task<string?> ResolveAsync(string secretRef, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(secretRef))
            return null;

        if (secretRef.StartsWith("appsecret:", StringComparison.OrdinalIgnoreCase))
        {
            var secretKey = secretRef.Substring("appsecret:".Length);
            if (string.IsNullOrWhiteSpace(secretKey))
                return null;

            var secret = await _appSecretStore.GetByKeyAsync(secretKey, ct);
            if (secret == null || string.IsNullOrWhiteSpace(secret.CipherText))
                return null;

            var cipherBytes = Convert.FromBase64String(secret.CipherText);
            var plainBytes = _protector.Unprotect(cipherBytes);
            return System.Text.Encoding.UTF8.GetString(plainBytes);
        }

        if (secretRef.StartsWith("env:", StringComparison.OrdinalIgnoreCase))
        {
            var envKey = secretRef.Substring(4);
            return Environment.GetEnvironmentVariable(envKey);
        }

        if (secretRef.StartsWith("config:", StringComparison.OrdinalIgnoreCase))
        {
            var configKey = secretRef.Substring(7);
            return _configuration[configKey];
        }

        return null;
    }
}
