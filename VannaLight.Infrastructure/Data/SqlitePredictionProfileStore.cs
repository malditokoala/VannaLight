using Dapper;
using Microsoft.Data.Sqlite;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Infrastructure.Data;

public sealed class SqlitePredictionProfileStore : IPredictionProfileStore
{
    public async Task<IReadOnlyList<PredictionProfile>> GetAllAsync(string sqlitePath, string domain, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection($"Data Source={sqlitePath};");
        await conn.OpenAsync(ct);

        const string sql = @"
SELECT
    Id,
    Domain,
    ProfileKey,
    DisplayName,
    DomainPackKey,
    TargetMetricKey,
    CalendarProfileKey,
    Grain,
    Horizon,
    HorizonUnit,
    ModelType,
    ConnectionName,
    SourceMode,
    TargetSeriesSource,
    FeatureSourcesJson,
    GroupByJson,
    FiltersJson,
    Notes,
    IsActive,
    CreatedUtc,
    UpdatedUtc
FROM PredictionProfiles
WHERE Domain = @Domain
ORDER BY IsActive DESC, ProfileKey ASC;";

        var rows = await conn.QueryAsync<PredictionProfile>(new CommandDefinition(sql, new { Domain = domain.Trim() }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<PredictionProfile?> GetAsync(string sqlitePath, string domain, string profileKey, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection($"Data Source={sqlitePath};");
        await conn.OpenAsync(ct);

        const string sql = @"
SELECT
    Id,
    Domain,
    ProfileKey,
    DisplayName,
    DomainPackKey,
    TargetMetricKey,
    CalendarProfileKey,
    Grain,
    Horizon,
    HorizonUnit,
    ModelType,
    ConnectionName,
    SourceMode,
    TargetSeriesSource,
    FeatureSourcesJson,
    GroupByJson,
    FiltersJson,
    Notes,
    IsActive,
    CreatedUtc,
    UpdatedUtc
FROM PredictionProfiles
WHERE Domain = @Domain
  AND ProfileKey = @ProfileKey
LIMIT 1;";

        return await conn.QueryFirstOrDefaultAsync<PredictionProfile>(
            new CommandDefinition(sql, new { Domain = domain.Trim(), ProfileKey = profileKey.Trim() }, cancellationToken: ct));
    }

    public async Task<long> UpsertAsync(string sqlitePath, PredictionProfile profile, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        using var conn = new SqliteConnection($"Data Source={sqlitePath};");
        await conn.OpenAsync(ct);

        var now = DateTime.UtcNow.ToString("O");
        var normalizedDomain = NormalizeRequired(profile.Domain);
        var normalizedProfileKey = NormalizeRequired(profile.ProfileKey);
        using var tx = await conn.BeginTransactionAsync(ct);

        if (profile.Id > 0)
        {
            if (profile.IsActive)
            {
                const string deactivateOthersSql = @"
UPDATE PredictionProfiles
SET
    IsActive = 0,
    UpdatedUtc = @UpdatedUtc
WHERE Domain = @Domain
  AND Id <> @Id;";

                await conn.ExecuteAsync(new CommandDefinition(
                    deactivateOthersSql,
                    new
                    {
                        Domain = normalizedDomain,
                        Id = profile.Id,
                        UpdatedUtc = now
                    },
                    transaction: tx,
                    cancellationToken: ct));
            }

            const string updateSql = @"
UPDATE PredictionProfiles
SET
    Domain = @Domain,
    ProfileKey = @ProfileKey,
    DisplayName = @DisplayName,
    DomainPackKey = @DomainPackKey,
    TargetMetricKey = @TargetMetricKey,
    CalendarProfileKey = @CalendarProfileKey,
    Grain = @Grain,
    Horizon = @Horizon,
    HorizonUnit = @HorizonUnit,
    ModelType = @ModelType,
    ConnectionName = @ConnectionName,
    SourceMode = @SourceMode,
    TargetSeriesSource = @TargetSeriesSource,
    FeatureSourcesJson = @FeatureSourcesJson,
    GroupByJson = @GroupByJson,
    FiltersJson = @FiltersJson,
    Notes = @Notes,
    IsActive = @IsActive,
    UpdatedUtc = @UpdatedUtc
WHERE Id = @Id;";

            var affected = await conn.ExecuteAsync(new CommandDefinition(updateSql, new
            {
                profile.Id,
                Domain = normalizedDomain,
                ProfileKey = normalizedProfileKey,
                DisplayName = NormalizeRequired(profile.DisplayName),
                DomainPackKey = NormalizeRequired(profile.DomainPackKey),
                TargetMetricKey = NormalizeRequired(profile.TargetMetricKey),
                CalendarProfileKey = NormalizeRequired(profile.CalendarProfileKey),
                Grain = NormalizeRequired(profile.Grain),
                profile.Horizon,
                HorizonUnit = NormalizeRequired(profile.HorizonUnit),
                ModelType = NormalizeRequired(profile.ModelType),
                ConnectionName = NormalizeOptional(profile.ConnectionName),
                SourceMode = NormalizeOptional(profile.SourceMode),
                TargetSeriesSource = NormalizeOptional(profile.TargetSeriesSource),
                FeatureSourcesJson = NormalizeOptional(profile.FeatureSourcesJson),
                GroupByJson = NormalizeOptional(profile.GroupByJson),
                FiltersJson = NormalizeOptional(profile.FiltersJson),
                Notes = NormalizeOptional(profile.Notes),
                IsActive = profile.IsActive ? 1 : 0,
                UpdatedUtc = now
            }, transaction: tx, cancellationToken: ct));

            await tx.CommitAsync(ct);

            return affected > 0 ? profile.Id : 0;
        }

        if (profile.IsActive)
        {
            const string deactivateOthersSql = @"
UPDATE PredictionProfiles
SET
    IsActive = 0,
    UpdatedUtc = @UpdatedUtc
WHERE Domain = @Domain;";

            await conn.ExecuteAsync(new CommandDefinition(
                deactivateOthersSql,
                new
                {
                    Domain = normalizedDomain,
                    UpdatedUtc = now
                },
                transaction: tx,
                cancellationToken: ct));
        }

        const string insertSql = @"
INSERT INTO PredictionProfiles
(
    Domain,
    ProfileKey,
    DisplayName,
    DomainPackKey,
    TargetMetricKey,
    CalendarProfileKey,
    Grain,
    Horizon,
    HorizonUnit,
    ModelType,
    ConnectionName,
    SourceMode,
    TargetSeriesSource,
    FeatureSourcesJson,
    GroupByJson,
    FiltersJson,
    Notes,
    IsActive,
    CreatedUtc,
    UpdatedUtc
)
VALUES
(
    @Domain,
    @ProfileKey,
    @DisplayName,
    @DomainPackKey,
    @TargetMetricKey,
    @CalendarProfileKey,
    @Grain,
    @Horizon,
    @HorizonUnit,
    @ModelType,
    @ConnectionName,
    @SourceMode,
    @TargetSeriesSource,
    @FeatureSourcesJson,
    @GroupByJson,
    @FiltersJson,
    @Notes,
    @IsActive,
    @CreatedUtc,
    @CreatedUtc
);
SELECT last_insert_rowid();";

        var insertedId = await conn.ExecuteScalarAsync<long>(new CommandDefinition(insertSql, new
        {
            Domain = normalizedDomain,
            ProfileKey = normalizedProfileKey,
            DisplayName = NormalizeRequired(profile.DisplayName),
            DomainPackKey = NormalizeRequired(profile.DomainPackKey),
            TargetMetricKey = NormalizeRequired(profile.TargetMetricKey),
            CalendarProfileKey = NormalizeRequired(profile.CalendarProfileKey),
            Grain = NormalizeRequired(profile.Grain),
            profile.Horizon,
            HorizonUnit = NormalizeRequired(profile.HorizonUnit),
            ModelType = NormalizeRequired(profile.ModelType),
            ConnectionName = NormalizeOptional(profile.ConnectionName),
            SourceMode = NormalizeOptional(profile.SourceMode),
            TargetSeriesSource = NormalizeOptional(profile.TargetSeriesSource),
            FeatureSourcesJson = NormalizeOptional(profile.FeatureSourcesJson),
            GroupByJson = NormalizeOptional(profile.GroupByJson),
            FiltersJson = NormalizeOptional(profile.FiltersJson),
            Notes = NormalizeOptional(profile.Notes),
            IsActive = profile.IsActive ? 1 : 0,
            CreatedUtc = now
        }, transaction: tx, cancellationToken: ct));

        await tx.CommitAsync(ct);
        return insertedId;
    }

    public async Task<bool> SetIsActiveAsync(string sqlitePath, long id, bool isActive, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection($"Data Source={sqlitePath};");
        await conn.OpenAsync(ct);

        const string sql = @"
UPDATE PredictionProfiles
SET
    IsActive = @IsActive,
    UpdatedUtc = @UpdatedUtc
WHERE Id = @Id;";

        var affected = await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            IsActive = isActive ? 1 : 0,
            UpdatedUtc = DateTime.UtcNow.ToString("O")
        }, cancellationToken: ct));

        return affected > 0;
    }

    public async Task<bool> SetActiveProfileAsync(string sqlitePath, string domain, long id, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection($"Data Source={sqlitePath};");
        await conn.OpenAsync(ct);
        using var tx = await conn.BeginTransactionAsync(ct);

        var normalizedDomain = NormalizeRequired(domain);
        var now = DateTime.UtcNow.ToString("O");

        const string deactivateSql = @"
UPDATE PredictionProfiles
SET
    IsActive = 0,
    UpdatedUtc = @UpdatedUtc
WHERE Domain = @Domain;";

        const string activateSql = @"
UPDATE PredictionProfiles
SET
    IsActive = 1,
    UpdatedUtc = @UpdatedUtc
WHERE Domain = @Domain
  AND Id = @Id;";

        await conn.ExecuteAsync(new CommandDefinition(
            deactivateSql,
            new { Domain = normalizedDomain, UpdatedUtc = now },
            transaction: tx,
            cancellationToken: ct));

        var affected = await conn.ExecuteAsync(new CommandDefinition(
            activateSql,
            new { Domain = normalizedDomain, Id = id, UpdatedUtc = now },
            transaction: tx,
            cancellationToken: ct));

        await tx.CommitAsync(ct);
        return affected > 0;
    }

    private static string NormalizeRequired(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Required value missing for prediction profile.");
        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
