using Dapper;
using Microsoft.Data.Sqlite;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Infrastructure.Data;

public sealed class SqliteAppSecretStore : IAppSecretStore
{
    private readonly string _sqlitePath;

    public SqliteAppSecretStore(string sqlitePath)
    {
        _sqlitePath = sqlitePath;
    }

    private SqliteConnection CreateConnection()
        => new($"Data Source={_sqlitePath}");

    public async Task<AppSecret?> GetByKeyAsync(string secretKey, CancellationToken ct = default)
    {
        const string sql = """
            SELECT *
            FROM AppSecrets
            WHERE SecretKey = @secretKey
            LIMIT 1;
            """;

        await using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<AppSecret>(
            new CommandDefinition(sql, new { secretKey }, cancellationToken: ct));
    }

    public async Task<int> UpsertAsync(AppSecret secret, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO AppSecrets
                (SecretKey, CipherText, Description, CreatedUtc, UpdatedUtc)
            VALUES
                (@SecretKey, @CipherText, @Description, @CreatedUtc, @UpdatedUtc)
            ON CONFLICT(SecretKey)
            DO UPDATE SET
                CipherText = excluded.CipherText,
                Description = excluded.Description,
                UpdatedUtc = excluded.UpdatedUtc;

            SELECT Id
            FROM AppSecrets
            WHERE SecretKey = @SecretKey;
            """;

        await using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(sql, secret, cancellationToken: ct));
    }
}
