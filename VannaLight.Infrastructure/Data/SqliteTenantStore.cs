using Dapper;
using Microsoft.Data.Sqlite;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Infrastructure.Data;

public class SqliteTenantStore : ITenantStore
{
    private readonly string _sqlitePath;

    public SqliteTenantStore(string sqlitePath)
    {
        _sqlitePath = sqlitePath;
    }

    private SqliteConnection CreateConnection()
        => new($"Data Source={_sqlitePath}");

    public async Task<Tenant?> GetByKeyAsync(string tenantKey, CancellationToken ct = default)
    {
        const string sql = """
            SELECT *
            FROM Tenants
            WHERE TenantKey = @tenantKey
            LIMIT 1;
            """;

        await using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<Tenant>(
            new CommandDefinition(sql, new { tenantKey }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT *
            FROM Tenants
            ORDER BY DisplayName, TenantKey;
            """;

        await using var conn = CreateConnection();
        var rows = await conn.QueryAsync<Tenant>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<int> UpsertAsync(Tenant tenant, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO Tenants
                (TenantKey, DisplayName, Description, IsActive, ManagementMode, CreatedUtc, UpdatedUtc)
            VALUES
                (@TenantKey, @DisplayName, @Description, @IsActive, @ManagementMode, @CreatedUtc, @UpdatedUtc)
            ON CONFLICT(TenantKey)
            DO UPDATE SET
                DisplayName = excluded.DisplayName,
                Description = excluded.Description,
                IsActive = excluded.IsActive,
                ManagementMode = excluded.ManagementMode,
                UpdatedUtc = excluded.UpdatedUtc;

            SELECT Id
            FROM Tenants
            WHERE TenantKey = @TenantKey;
            """;

        await using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(sql, tenant, cancellationToken: ct));
    }
}
