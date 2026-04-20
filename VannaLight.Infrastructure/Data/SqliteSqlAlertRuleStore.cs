using Dapper;
using Microsoft.Data.Sqlite;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Infrastructure.Data;

public sealed class SqliteSqlAlertRuleStore : ISqlAlertRuleStore
{
    private readonly string _sqlitePath;

    public SqliteSqlAlertRuleStore(string sqlitePath)
    {
        _sqlitePath = sqlitePath;
    }

    private SqliteConnection CreateConnection() => new($"Data Source={_sqlitePath}");

    public async Task<IReadOnlyList<SqlAlertRule>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = @"
SELECT
    Id,
    RuleKey,
    TenantKey,
    Domain,
    ConnectionName,
    DisplayName,
    MetricKey,
    DimensionKey,
    DimensionValue,
    ComparisonOperator,
    Threshold,
    TimeScope,
    EvaluationFrequencyMinutes,
    CooldownMinutes,
    IsActive,
    Notes,
    CreatedUtc,
    UpdatedUtc
FROM SqlAlertRules
ORDER BY IsActive DESC, UpdatedUtc DESC, DisplayName ASC;";

        await using var conn = CreateConnection();
        var rows = await conn.QueryAsync<SqlAlertRule>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<SqlAlertRule>> GetActiveAsync(CancellationToken ct = default)
    {
        const string sql = @"
SELECT
    Id,
    RuleKey,
    TenantKey,
    Domain,
    ConnectionName,
    DisplayName,
    MetricKey,
    DimensionKey,
    DimensionValue,
    ComparisonOperator,
    Threshold,
    TimeScope,
    EvaluationFrequencyMinutes,
    CooldownMinutes,
    IsActive,
    Notes,
    CreatedUtc,
    UpdatedUtc
FROM SqlAlertRules
WHERE IsActive = 1
ORDER BY UpdatedUtc DESC, DisplayName ASC;";

        await using var conn = CreateConnection();
        var rows = await conn.QueryAsync<SqlAlertRule>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<SqlAlertRule?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        const string sql = @"
SELECT
    Id,
    RuleKey,
    TenantKey,
    Domain,
    ConnectionName,
    DisplayName,
    MetricKey,
    DimensionKey,
    DimensionValue,
    ComparisonOperator,
    Threshold,
    TimeScope,
    EvaluationFrequencyMinutes,
    CooldownMinutes,
    IsActive,
    Notes,
    CreatedUtc,
    UpdatedUtc
FROM SqlAlertRules
WHERE Id = @Id
LIMIT 1;";

        await using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<SqlAlertRule>(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }

    public async Task<long> UpsertAsync(SqlAlertRule rule, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(rule);
        rule.ThrowIfInvalid();

        var now = DateTime.UtcNow.ToString("O");
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        if (rule.Id > 0)
        {
            const string updateSql = @"
UPDATE SqlAlertRules
SET
    RuleKey = @RuleKey,
    TenantKey = @TenantKey,
    Domain = @Domain,
    ConnectionName = @ConnectionName,
    DisplayName = @DisplayName,
    MetricKey = @MetricKey,
    DimensionKey = @DimensionKey,
    DimensionValue = @DimensionValue,
    ComparisonOperator = @ComparisonOperator,
    Threshold = @Threshold,
    TimeScope = @TimeScope,
    EvaluationFrequencyMinutes = @EvaluationFrequencyMinutes,
    CooldownMinutes = @CooldownMinutes,
    IsActive = @IsActive,
    Notes = @Notes,
    UpdatedUtc = @UpdatedUtc
WHERE Id = @Id;";

            await conn.ExecuteAsync(new CommandDefinition(updateSql, new
            {
                rule.Id,
                RuleKey = NormalizeRequired(rule.RuleKey),
                TenantKey = NormalizeRequired(rule.TenantKey),
                Domain = NormalizeRequired(rule.Domain),
                ConnectionName = NormalizeRequired(rule.ConnectionName),
                DisplayName = NormalizeRequired(rule.DisplayName),
                MetricKey = NormalizeRequired(rule.MetricKey),
                DimensionKey = NormalizeOptional(rule.DimensionKey),
                DimensionValue = NormalizeOptional(rule.DimensionValue),
                ComparisonOperator = (int)rule.ComparisonOperator,
                rule.Threshold,
                TimeScope = (int)rule.TimeScope,
                rule.EvaluationFrequencyMinutes,
                rule.CooldownMinutes,
                IsActive = rule.IsActive ? 1 : 0,
                Notes = NormalizeOptional(rule.Notes),
                UpdatedUtc = now
            }, cancellationToken: ct));

            return rule.Id;
        }

        const string insertSql = @"
INSERT INTO SqlAlertRules
(
    RuleKey,
    TenantKey,
    Domain,
    ConnectionName,
    DisplayName,
    MetricKey,
    DimensionKey,
    DimensionValue,
    ComparisonOperator,
    Threshold,
    TimeScope,
    EvaluationFrequencyMinutes,
    CooldownMinutes,
    IsActive,
    Notes,
    CreatedUtc,
    UpdatedUtc
)
VALUES
(
    @RuleKey,
    @TenantKey,
    @Domain,
    @ConnectionName,
    @DisplayName,
    @MetricKey,
    @DimensionKey,
    @DimensionValue,
    @ComparisonOperator,
    @Threshold,
    @TimeScope,
    @EvaluationFrequencyMinutes,
    @CooldownMinutes,
    @IsActive,
    @Notes,
    @CreatedUtc,
    @CreatedUtc
);
SELECT last_insert_rowid();";

        return await conn.ExecuteScalarAsync<long>(new CommandDefinition(insertSql, new
        {
            RuleKey = NormalizeRequired(rule.RuleKey),
            TenantKey = NormalizeRequired(rule.TenantKey),
            Domain = NormalizeRequired(rule.Domain),
            ConnectionName = NormalizeRequired(rule.ConnectionName),
            DisplayName = NormalizeRequired(rule.DisplayName),
            MetricKey = NormalizeRequired(rule.MetricKey),
            DimensionKey = NormalizeOptional(rule.DimensionKey),
            DimensionValue = NormalizeOptional(rule.DimensionValue),
            ComparisonOperator = (int)rule.ComparisonOperator,
            rule.Threshold,
            TimeScope = (int)rule.TimeScope,
            rule.EvaluationFrequencyMinutes,
            rule.CooldownMinutes,
            IsActive = rule.IsActive ? 1 : 0,
            Notes = NormalizeOptional(rule.Notes),
            CreatedUtc = now
        }, cancellationToken: ct));
    }

    public async Task<bool> SetIsActiveAsync(long id, bool isActive, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE SqlAlertRules
SET
    IsActive = @IsActive,
    UpdatedUtc = @UpdatedUtc
WHERE Id = @Id;";

        await using var conn = CreateConnection();
        var affected = await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            IsActive = isActive ? 1 : 0,
            UpdatedUtc = DateTime.UtcNow.ToString("O")
        }, cancellationToken: ct));

        return affected > 0;
    }

    private static string NormalizeRequired(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("Falta un valor obligatorio en la regla de alerta SQL.");
        return trimmed;
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
