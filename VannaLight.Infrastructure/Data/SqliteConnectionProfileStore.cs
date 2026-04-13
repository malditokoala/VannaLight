using Dapper;
using Microsoft.Data.Sqlite;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Infrastructure.Data;

public class SqliteConnectionProfileStore : IConnectionProfileStore
{
    private readonly string _sqlitePath;

    public SqliteConnectionProfileStore(string sqlitePath)
    {
        _sqlitePath = sqlitePath;
    }

    private SqliteConnection CreateConnection()
        => new SqliteConnection($"Data Source={_sqlitePath}");

    public async Task<ConnectionProfile?> GetActiveAsync(string environmentName, string connectionName, CancellationToken ct = default)
    {
        const string sql = @"
                            SELECT *
                            FROM ConnectionProfiles
                            WHERE EnvironmentName = @environmentName
                              AND ConnectionName = @connectionName
                              AND IsActive = 1
                            LIMIT 1;";

        await using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<ConnectionProfile>(
            new CommandDefinition(sql, new { environmentName, connectionName }, cancellationToken: ct));
    }

    public async Task<ConnectionProfile?> GetAsync(string environmentName, string profileKey, string connectionName, CancellationToken ct = default)
    {
        const string sql = @"
                            SELECT *
                            FROM ConnectionProfiles
                            WHERE EnvironmentName = @environmentName
                              AND ProfileKey = @profileKey
                              AND ConnectionName = @connectionName
                            LIMIT 1;";

        await using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<ConnectionProfile>(
            new CommandDefinition(sql, new { environmentName, profileKey, connectionName }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<ConnectionProfile>> GetAllAsync(string environmentName, CancellationToken ct = default)
    {
        const string sql = @"
                            SELECT *
                            FROM ConnectionProfiles
                            WHERE EnvironmentName = @environmentName
                            ORDER BY IsActive DESC, ConnectionName, ProfileKey;";

        await using var conn = CreateConnection();
        var rows = await conn.QueryAsync<ConnectionProfile>(
            new CommandDefinition(sql, new { environmentName }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<int> UpsertAsync(ConnectionProfile profile, CancellationToken ct = default)
    {
        const string sql = @"
                            INSERT INTO ConnectionProfiles
                            (EnvironmentName, ProfileKey, ConnectionName, ProviderKind, ConnectionMode, ServerHost, DatabaseName, UserName,
                             IntegratedSecurity, Encrypt, TrustServerCertificate, CommandTimeoutSec, SecretRef, IsActive, Description, ManagementMode, CreatedUtc, UpdatedUtc)
                            VALUES
                            (@EnvironmentName, @ProfileKey, @ConnectionName, @ProviderKind, @ConnectionMode, @ServerHost, @DatabaseName, @UserName,
                             @IntegratedSecurity, @Encrypt, @TrustServerCertificate, @CommandTimeoutSec, @SecretRef, @IsActive, @Description, @ManagementMode, @CreatedUtc, @UpdatedUtc)
                            ON CONFLICT(EnvironmentName, ProfileKey, ConnectionName)
                            DO UPDATE SET
                                ProviderKind = excluded.ProviderKind,
                                ConnectionMode = excluded.ConnectionMode,
                                ServerHost = excluded.ServerHost,
                                DatabaseName = excluded.DatabaseName,
                                UserName = excluded.UserName,
                                IntegratedSecurity = excluded.IntegratedSecurity,
                                Encrypt = excluded.Encrypt,
                                TrustServerCertificate = excluded.TrustServerCertificate,
                                CommandTimeoutSec = excluded.CommandTimeoutSec,
                                SecretRef = excluded.SecretRef,
                                IsActive = excluded.IsActive,
                                Description = excluded.Description,
                                ManagementMode = excluded.ManagementMode,
                                UpdatedUtc = excluded.UpdatedUtc;

                            SELECT Id
                            FROM ConnectionProfiles
                            WHERE EnvironmentName = @EnvironmentName
                              AND ProfileKey = @ProfileKey
                              AND ConnectionName = @ConnectionName;";

        await using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(sql, profile, cancellationToken: ct));
    }
}
