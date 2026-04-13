using Dapper;
using Microsoft.Data.Sqlite;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Infrastructure.Data;

public class SqliteTenantDomainStore : ITenantDomainStore
{
    private readonly string _sqlitePath;

    public SqliteTenantDomainStore(string sqlitePath)
    {
        _sqlitePath = sqlitePath;
    }

    private SqliteConnection CreateConnection()
        => new($"Data Source={_sqlitePath}");

    public async Task<TenantDomain?> GetDefaultByTenantAsync(string tenantKey, CancellationToken ct = default)
    {
        const string sql = """
            SELECT td.*
            FROM TenantDomains td
            INNER JOIN Tenants t ON t.Id = td.TenantId
            WHERE t.TenantKey = @tenantKey
              AND t.IsActive = 1
              AND td.IsActive = 1
              AND td.IsDefault = 1
            LIMIT 1;
            """;

        await using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<TenantDomain>(
            new CommandDefinition(sql, new { tenantKey }, cancellationToken: ct));
    }

    public async Task<TenantDomain?> GetByTenantAndDomainAsync(string tenantKey, string domain, CancellationToken ct = default)
    {
        const string sql = """
            SELECT td.*
            FROM TenantDomains td
            INNER JOIN Tenants t ON t.Id = td.TenantId
            WHERE t.TenantKey = @tenantKey
              AND td.Domain = @domain
              AND t.IsActive = 1
              AND td.IsActive = 1
            LIMIT 1;
            """;

        await using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<TenantDomain>(
            new CommandDefinition(sql, new { tenantKey, domain }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<TenantDomain>> GetAllByTenantAsync(string tenantKey, CancellationToken ct = default)
    {
        const string sql = """
            SELECT td.*
            FROM TenantDomains td
            INNER JOIN Tenants t ON t.Id = td.TenantId
            WHERE t.TenantKey = @tenantKey
            ORDER BY td.IsDefault DESC, td.Domain;
            """;

        await using var conn = CreateConnection();
        var rows = await conn.QueryAsync<TenantDomain>(
            new CommandDefinition(sql, new { tenantKey }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<int> UpsertAsync(TenantDomain tenantDomain, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE TenantDomains
            SET IsDefault = 0,
                UpdatedUtc = @UpdatedUtc
            WHERE TenantId = @TenantId
              AND Domain <> @Domain
              AND @IsDefault = 1;

            INSERT INTO TenantDomains
                (TenantId, Domain, ConnectionName, SystemProfileKey, IsDefault, IsActive, ManagementMode, CreatedUtc, UpdatedUtc)
            VALUES
                (@TenantId, @Domain, @ConnectionName, @SystemProfileKey, @IsDefault, @IsActive, @ManagementMode, @CreatedUtc, @UpdatedUtc)
            ON CONFLICT(TenantId, Domain)
            DO UPDATE SET
                ConnectionName = excluded.ConnectionName,
                SystemProfileKey = excluded.SystemProfileKey,
                IsDefault = excluded.IsDefault,
                IsActive = excluded.IsActive,
                ManagementMode = excluded.ManagementMode,
                UpdatedUtc = excluded.UpdatedUtc;

            SELECT Id
            FROM TenantDomains
            WHERE TenantId = @TenantId
              AND Domain = @Domain;
            """;

        await using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(sql, tenantDomain, cancellationToken: ct));
    }
}
