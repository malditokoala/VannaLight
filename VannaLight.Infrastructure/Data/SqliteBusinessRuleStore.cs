using Dapper;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Infrastructure.Data;

public class SqliteBusinessRuleStore : IBusinessRuleStore
{
    public async Task<IReadOnlyList<BusinessRule>> GetActiveRulesAsync(
        string sqlitePath,
        string domain,
        int maxRules,
        CancellationToken ct = default)
    {
        using var conn = new SqliteConnection($"Data Source={sqlitePath};");
        await conn.OpenAsync(ct);

        const string sql = @"
                            SELECT
                                Id,
                                Domain,
                                RuleKey,
                                RuleText,
                                Priority,
                                IsActive
                            FROM BusinessRules
                            WHERE Domain = @Domain
                              AND IsActive = 1
                            ORDER BY Priority ASC
                            LIMIT @MaxRules;";

        var rows = await conn.QueryAsync<BusinessRule>(sql, new
        {
            Domain = domain,
            MaxRules = maxRules
        });

        return rows.AsList();
    }

    public async Task<IReadOnlyList<BusinessRule>> GetAllRulesAsync(
        string sqlitePath,
        string domain,
        CancellationToken ct = default)
    {
        using var conn = new SqliteConnection($"Data Source={sqlitePath};");
        await conn.OpenAsync(ct);

        const string sql = @"
                            SELECT
                                Id,
                                Domain,
                                RuleKey,
                                RuleText,
                                Priority,
                                IsActive
                            FROM BusinessRules
                            WHERE Domain = @Domain
                            ORDER BY Priority ASC, Id ASC;";

        var rows = await conn.QueryAsync<BusinessRule>(sql, new
        {
            Domain = domain
        });

        return rows.AsList();
    }

    public async Task<long> UpsertAsync(
        string sqlitePath,
        BusinessRule rule,
        CancellationToken ct = default)
    {
        using var conn = new SqliteConnection($"Data Source={sqlitePath};");
        await conn.OpenAsync(ct);

        if (rule.Id > 0)
        {
            const string updateSql = @"
                                        UPDATE BusinessRules
                                        SET
                                            Domain = @Domain,
                                            RuleKey = @RuleKey,
                                            RuleText = @RuleText,
                                            Priority = @Priority,
                                            IsActive = @IsActive,
                                            UpdatedUtc = @UpdatedUtc
                                        WHERE Id = @Id;";

            var affected = await conn.ExecuteAsync(updateSql, new
            {
                rule.Id,
                rule.Domain,
                rule.RuleKey,
                rule.RuleText,
                rule.Priority,
                IsActive = rule.IsActive ? 1 : 0,
                UpdatedUtc = DateTime.UtcNow
            });

            return affected > 0 ? rule.Id : 0;
        }

        const string insertSql = @"
                                    INSERT INTO BusinessRules
                                    (
                                        Domain,
                                        RuleKey,
                                        RuleText,
                                        Priority,
                                        IsActive,
                                        CreatedUtc
                                    )
                                    VALUES
                                    (
                                        @Domain,
                                        @RuleKey,
                                        @RuleText,
                                        @Priority,
                                        @IsActive,
                                        @CreatedUtc
                                    );

                                    SELECT last_insert_rowid();";

        var newId = await conn.ExecuteScalarAsync<long>(insertSql, new
        {
            rule.Domain,
            rule.RuleKey,
            rule.RuleText,
            rule.Priority,
            IsActive = rule.IsActive ? 1 : 0,
            CreatedUtc = DateTime.UtcNow
        });

        return newId;
    }

    public async Task<bool> SetIsActiveAsync(
        string sqlitePath,
        long id,
        bool isActive,
        CancellationToken ct = default)
    {
        using var conn = new SqliteConnection($"Data Source={sqlitePath};");
        await conn.OpenAsync(ct);

        const string sql = @"
                            UPDATE BusinessRules
                            SET
                                IsActive = @IsActive,
                                UpdatedUtc = @UpdatedUtc
                            WHERE Id = @Id;";

        var affected = await conn.ExecuteAsync(sql, new
        {
            Id = id,
            IsActive = isActive ? 1 : 0,
            UpdatedUtc = DateTime.UtcNow
        });

        return affected > 0;
    }
}