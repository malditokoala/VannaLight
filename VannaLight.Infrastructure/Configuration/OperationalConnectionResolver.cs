using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using VannaLight.Core.Abstractions;


namespace VannaLight.Infrastructure.Configuration;

public class OperationalConnectionResolver : IOperationalConnectionResolver
{
    private readonly IConnectionProfileStore _connectionProfileStore;
    private readonly ISecretResolver _secretResolver;
    private readonly IConfiguration _configuration;
    private readonly string _environmentName;

    public OperationalConnectionResolver(
        IConnectionProfileStore connectionProfileStore,
        ISecretResolver secretResolver,
        IConfiguration configuration)
    {
        _connectionProfileStore = connectionProfileStore;
        _secretResolver = secretResolver;
        _configuration = configuration;
        _environmentName = configuration["Bootstrap:EnvironmentName"] ?? "Development";
    }

    public async Task<string> ResolveOperationalConnectionStringAsync(CancellationToken ct = default)
    {
        var profile = await _connectionProfileStore.GetActiveAsync(_environmentName, "OperationalDb", ct);
        if (profile == null)
        {
            var fallback = _configuration.GetConnectionString("OperationalDb");
            if (string.IsNullOrWhiteSpace(fallback))
                throw new InvalidOperationException("OperationalDb connection not configured.");
            return fallback;
        }

        if (string.Equals(profile.ConnectionMode, "FullStringRef", StringComparison.OrdinalIgnoreCase))
        {
            var fullConnection = await _secretResolver.ResolveAsync(profile.SecretRef ?? string.Empty, ct);
            if (string.IsNullOrWhiteSpace(fullConnection))
                throw new InvalidOperationException("OperationalDb secret reference could not be resolved.");

            return fullConnection;
        }

        if (string.Equals(profile.ConnectionMode, "CompositeSqlServer", StringComparison.OrdinalIgnoreCase))
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = profile.ServerHost,
                InitialCatalog = profile.DatabaseName,
                IntegratedSecurity = profile.IntegratedSecurity,
                Encrypt = profile.Encrypt,
                TrustServerCertificate = profile.TrustServerCertificate
            };

            if (!profile.IntegratedSecurity)
            {
                builder.UserID = profile.UserName;
                builder.Password = await _secretResolver.ResolveAsync(profile.SecretRef ?? string.Empty, ct)
                    ?? throw new InvalidOperationException("OperationalDb password secret reference could not be resolved.");
            }

            return builder.ConnectionString;
        }

        throw new InvalidOperationException($"Unsupported connection mode: {profile.ConnectionMode}");
    }
}