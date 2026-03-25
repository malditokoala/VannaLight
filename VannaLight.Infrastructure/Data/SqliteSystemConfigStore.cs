using Dapper;
using Microsoft.Data.Sqlite;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Infrastructure.Data;

public class SqliteSystemConfigStore : ISystemConfigStore
{
    private readonly string _sqlitePath;

    public SqliteSystemConfigStore(string sqlitePath)
    {
        _sqlitePath = sqlitePath;
    }

    private SqliteConnection CreateConnection()
        => new SqliteConnection($"Data Source={_sqlitePath}");

    public async Task<SystemConfigProfile?> GetActiveProfileAsync(string environmentName, CancellationToken ct = default)
    {
        const string sql = @"
                            SELECT *
                            FROM SystemConfigProfiles
                            WHERE EnvironmentName = @environmentName
                              AND IsActive = 1
                            LIMIT 1;";

        await using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<SystemConfigProfile>(new CommandDefinition(sql, new { environmentName }, cancellationToken: ct));
    }

    public async Task<SystemConfigProfile?> GetProfileAsync(string environmentName, string profileKey, CancellationToken ct = default)
    {
        const string sql = @"
                            SELECT *
                            FROM SystemConfigProfiles
                            WHERE EnvironmentName = @environmentName
                              AND ProfileKey = @profileKey
                            LIMIT 1;";

        await using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<SystemConfigProfile>(
            new CommandDefinition(sql, new { environmentName, profileKey }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<SystemConfigEntry>> GetEntriesAsync(int profileId, CancellationToken ct = default)
    {
        const string sql = @"
                            SELECT *
                            FROM SystemConfigEntries
                            WHERE ProfileId = @profileId
                            ORDER BY Section, [Key];";

        await using var conn = CreateConnection();
        var rows = await conn.QueryAsync<SystemConfigEntry>(new CommandDefinition(sql, new { profileId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<SystemConfigEntry?> GetEntryAsync(int profileId, string section, string key, CancellationToken ct = default)
    {
        const string sql = @"
                            SELECT *
                            FROM SystemConfigEntries
                            WHERE ProfileId = @profileId
                              AND Section = @section
                              AND [Key] = @key
                            LIMIT 1;";

        await using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<SystemConfigEntry>(
            new CommandDefinition(sql, new { profileId, section, key }, cancellationToken: ct));
    }

    public async Task<int> UpsertProfileAsync(SystemConfigProfile profile, CancellationToken ct = default)
    {
        const string sql = @"
                            INSERT INTO SystemConfigProfiles
                            (EnvironmentName, ProfileKey, DisplayName, Description, IsActive, IsReadOnly, CreatedUtc, UpdatedUtc)
                            VALUES
                            (@EnvironmentName, @ProfileKey, @DisplayName, @Description, @IsActive, @IsReadOnly, @CreatedUtc, @UpdatedUtc)
                            ON CONFLICT(EnvironmentName, ProfileKey)
                            DO UPDATE SET
                                DisplayName = excluded.DisplayName,
                                Description = excluded.Description,
                                IsActive = excluded.IsActive,
                                IsReadOnly = excluded.IsReadOnly,
                                UpdatedUtc = excluded.UpdatedUtc;

                            SELECT Id
                            FROM SystemConfigProfiles
                            WHERE EnvironmentName = @EnvironmentName
                              AND ProfileKey = @ProfileKey;";

        await using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(sql, profile, cancellationToken: ct));
    }

    public async Task<int> UpsertEntryAsync(SystemConfigEntry entry, CancellationToken ct = default)
    {
        const string sql = @"
                            INSERT INTO SystemConfigEntries
                            (ProfileId, Section, [Key], Value, ValueType, IsSecret, SecretRef, IsEditableInUi, ValidationRule, Description, CreatedUtc, UpdatedUtc)
                            VALUES
                            (@ProfileId, @Section, @Key, @Value, @ValueType, @IsSecret, @SecretRef, @IsEditableInUi, @ValidationRule, @Description, @CreatedUtc, @UpdatedUtc)
                            ON CONFLICT(ProfileId, Section, [Key])
                            DO UPDATE SET
                                Value = excluded.Value,
                                ValueType = excluded.ValueType,
                                IsSecret = excluded.IsSecret,
                                SecretRef = excluded.SecretRef,
                                IsEditableInUi = excluded.IsEditableInUi,
                                ValidationRule = excluded.ValidationRule,
                                Description = excluded.Description,
                                UpdatedUtc = excluded.UpdatedUtc;

                            SELECT Id
                            FROM SystemConfigEntries
                            WHERE ProfileId = @ProfileId
                              AND Section = @Section
                              AND [Key] = @Key;";

        await using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(sql, entry, cancellationToken: ct));
    }
}