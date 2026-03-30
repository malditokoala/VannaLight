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
    private readonly string _defaultProfileKey;

    public OperationalConnectionResolver(
        IConnectionProfileStore connectionProfileStore,
        ISecretResolver secretResolver,
        IConfiguration configuration)
    {
        _connectionProfileStore = connectionProfileStore;
        _secretResolver = secretResolver;
        _configuration = configuration;
        _environmentName = configuration["SystemStartup:EnvironmentName"] ?? "Development";
        _defaultProfileKey = configuration["SystemStartup:DefaultSystemProfile"] ?? "default";
    }

    public async Task<string> ResolveOperationalConnectionStringAsync(CancellationToken ct = default)
        => await ResolveConnectionStringAsync("OperationalDb", ct);

    public async Task<string> ResolveConnectionStringAsync(string connectionName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(connectionName))
            throw new InvalidOperationException("ConnectionName is required.");

        var normalizedConnectionName = connectionName.Trim();

        var profile = await _connectionProfileStore.GetActiveAsync(_environmentName, normalizedConnectionName, ct)
            ?? await _connectionProfileStore.GetAsync(_environmentName, _defaultProfileKey, normalizedConnectionName, ct);
        if (profile == null)
        {
            var fallback = _configuration.GetConnectionString(normalizedConnectionName);
            if (string.IsNullOrWhiteSpace(fallback))
                throw new InvalidOperationException($"{normalizedConnectionName} connection not configured.");
            return fallback;
        }

        if (!string.Equals(profile.ProviderKind, "SqlServer", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unsupported provider kind: {profile.ProviderKind}");

        if (string.Equals(profile.ConnectionMode, "FullStringRef", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(profile.SecretRef))
                throw new InvalidOperationException("OperationalDb FullStringRef requires SecretRef.");

            var fullConnection = await _secretResolver.ResolveAsync(profile.SecretRef ?? string.Empty, ct);
            if (string.IsNullOrWhiteSpace(fullConnection))
                throw new InvalidOperationException("OperationalDb secret reference could not be resolved.");

            return fullConnection;
        }

        if (string.Equals(profile.ConnectionMode, "CompositeSqlServer", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(profile.ServerHost))
                throw new InvalidOperationException("OperationalDb composite profile requires ServerHost.");

            if (string.IsNullOrWhiteSpace(profile.DatabaseName))
                throw new InvalidOperationException("OperationalDb composite profile requires DatabaseName.");

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
                if (string.IsNullOrWhiteSpace(profile.UserName))
                    throw new InvalidOperationException("OperationalDb composite profile requires UserName when IntegratedSecurity is false.");

                if (string.IsNullOrWhiteSpace(profile.SecretRef))
                    throw new InvalidOperationException("OperationalDb composite profile requires SecretRef when IntegratedSecurity is false.");

                builder.UserID = profile.UserName;
                builder.Password = await _secretResolver.ResolveAsync(profile.SecretRef ?? string.Empty, ct)
                    ?? throw new InvalidOperationException("OperationalDb password secret reference could not be resolved.");
            }

            return builder.ConnectionString;
        }

        throw new InvalidOperationException($"Unsupported connection mode: {profile.ConnectionMode}");
    }
}
