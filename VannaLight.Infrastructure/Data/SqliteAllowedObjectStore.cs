using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;
using VannaLight.Core.Settings;

namespace VannaLight.Infrastructure.Data;

public sealed class SqliteAllowedObjectStore : IAllowedObjectStore
{
    private const string CachePrefix = "allowed-objects:";

    private readonly string _readConnectionString;
    private readonly string _writeConnectionString;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SqliteAllowedObjectStore> _logger;

    public SqliteAllowedObjectStore(
        IOptions<SqliteOptions> sqliteOptions,
        IMemoryCache cache,
        ILogger<SqliteAllowedObjectStore> logger)
    {
        if (sqliteOptions == null)
            throw new ArgumentNullException(nameof(sqliteOptions));

        if (sqliteOptions.Value == null)
            throw new ArgumentNullException(nameof(sqliteOptions.Value));

        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var dbPath = sqliteOptions.Value.DbPath;

        if (string.IsNullOrWhiteSpace(dbPath))
        {
            throw new InvalidOperationException("SqliteOptions.DbPath no está configurado.");
        }

        if (!Path.IsPathRooted(dbPath))
        {
            dbPath = Path.GetFullPath(dbPath);
        }

        _readConnectionString = $"Data Source={dbPath};Mode=ReadOnly;";
        _writeConnectionString = $"Data Source={dbPath};Mode=ReadWriteCreate;";
    }

    public async Task<IReadOnlyList<AllowedObject>> GetActiveObjectsAsync(
        string domain,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            throw new ArgumentException("Domain es requerido.", nameof(domain));
        }

        var normalizedDomain = NormalizeDomain(domain);
        var cacheKey = BuildCacheKey(normalizedDomain);

        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<AllowedObject>? cached) &&
            cached != null)
        {
            return cached;
        }

        try
        {
            await using var connection = new SqliteConnection(_readConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
SELECT
    Id,
    Domain,
    SchemaName,
    ObjectName,
    ObjectType,
    IsActive,
    Notes
FROM AllowedObjects
WHERE LOWER(TRIM(Domain)) = @Domain
  AND IsActive = 1
ORDER BY SchemaName, ObjectName;";

            var rows = (await connection.QueryAsync<AllowedObject>(
                new CommandDefinition(
                    sql,
                    new { Domain = normalizedDomain },
                    cancellationToken: cancellationToken)))
                .ToList();

            _cache.Set(
                cacheKey,
                rows,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                });

            _logger.LogInformation(
                "Loaded {Count} active allowed objects for domain {Domain}.",
                rows.Count,
                normalizedDomain);

            return rows;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error loading active AllowedObjects for domain {Domain}. Returning empty result (fail-closed).",
                normalizedDomain);

            return Array.Empty<AllowedObject>();
        }
    }

    public async Task<bool> IsAllowedAsync(
        string domain,
        string schemaName,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(domain) ||
            string.IsNullOrWhiteSpace(schemaName) ||
            string.IsNullOrWhiteSpace(objectName))
        {
            return false;
        }

        var rows = await GetActiveObjectsAsync(domain, cancellationToken);

        return rows.Any(x =>
            string.Equals(x.SchemaName, schemaName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.ObjectName, objectName, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<AllowedObject>> GetAllObjectsAsync(
        string domain,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            throw new ArgumentException("Domain es requerido.", nameof(domain));
        }

        var normalizedDomain = NormalizeDomain(domain);

        await using var connection = new SqliteConnection(_readConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
SELECT
    Id,
    Domain,
    SchemaName,
    ObjectName,
    ObjectType,
    IsActive,
    Notes
FROM AllowedObjects
WHERE LOWER(TRIM(Domain)) = @Domain
ORDER BY IsActive DESC, SchemaName, ObjectName;";

        var rows = await connection.QueryAsync<AllowedObject>(
            new CommandDefinition(
                sql,
                new { Domain = normalizedDomain },
                cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task<long> UpsertAsync(
        AllowedObject allowedObject,
        CancellationToken cancellationToken = default)
    {
        if (allowedObject == null)
        {
            throw new ArgumentNullException(nameof(allowedObject));
        }

        if (string.IsNullOrWhiteSpace(allowedObject.Domain))
        {
            throw new ArgumentException("Domain es requerido.", nameof(allowedObject));
        }

        if (string.IsNullOrWhiteSpace(allowedObject.SchemaName))
        {
            throw new ArgumentException("SchemaName es requerido.", nameof(allowedObject));
        }

        if (string.IsNullOrWhiteSpace(allowedObject.ObjectName))
        {
            throw new ArgumentException("ObjectName es requerido.", nameof(allowedObject));
        }

        var normalizedDomain = NormalizeDomain(allowedObject.Domain);
        var normalizedSchemaName = allowedObject.SchemaName.Trim();
        var normalizedObjectName = allowedObject.ObjectName.Trim();
        var normalizedObjectType = string.IsNullOrWhiteSpace(allowedObject.ObjectType)
            ? string.Empty
            : allowedObject.ObjectType.Trim();
        var normalizedNotes = string.IsNullOrWhiteSpace(allowedObject.Notes)
            ? null
            : allowedObject.Notes.Trim();

        await using var connection = new SqliteConnection(_writeConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string findSql = @"
SELECT Id
FROM AllowedObjects
WHERE LOWER(TRIM(Domain)) = @Domain
  AND LOWER(TRIM(SchemaName)) = LOWER(TRIM(@SchemaName))
  AND LOWER(TRIM(ObjectName)) = LOWER(TRIM(@ObjectName))
LIMIT 1;";

        var existingId = await connection.ExecuteScalarAsync<long?>(
            new CommandDefinition(
                findSql,
                new
                {
                    Domain = normalizedDomain,
                    SchemaName = normalizedSchemaName,
                    ObjectName = normalizedObjectName
                },
                cancellationToken: cancellationToken));

        if (existingId.HasValue)
        {
            const string updateSql = @"
UPDATE AllowedObjects
SET ObjectType = @ObjectType,
    IsActive = @IsActive,
    Notes = @Notes
WHERE Id = @Id;";

            await connection.ExecuteAsync(
                new CommandDefinition(
                    updateSql,
                    new
                    {
                        Id = existingId.Value,
                        ObjectType = normalizedObjectType,
                        IsActive = allowedObject.IsActive ? 1 : 0,
                        Notes = normalizedNotes
                    },
                    cancellationToken: cancellationToken));

            InvalidateDomainCache(normalizedDomain);

            _logger.LogInformation(
                "Updated AllowedObject Id {Id} for domain {Domain}: {Schema}.{Object}.",
                existingId.Value,
                normalizedDomain,
                normalizedSchemaName,
                normalizedObjectName);

            return existingId.Value;
        }

        const string insertSql = @"
INSERT INTO AllowedObjects
(
    Domain,
    SchemaName,
    ObjectName,
    ObjectType,
    IsActive,
    Notes
)
VALUES
(
    @Domain,
    @SchemaName,
    @ObjectName,
    @ObjectType,
    @IsActive,
    @Notes
);
SELECT last_insert_rowid();";

        var newId = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                insertSql,
                new
                {
                    Domain = normalizedDomain,
                    SchemaName = normalizedSchemaName,
                    ObjectName = normalizedObjectName,
                    ObjectType = normalizedObjectType,
                    IsActive = allowedObject.IsActive ? 1 : 0,
                    Notes = normalizedNotes
                },
                cancellationToken: cancellationToken));

        InvalidateDomainCache(normalizedDomain);

        _logger.LogInformation(
            "Inserted AllowedObject Id {Id} for domain {Domain}: {Schema}.{Object}.",
            newId,
            normalizedDomain,
            normalizedSchemaName,
            normalizedObjectName);

        return newId;
    }

    public async Task<bool> SetIsActiveAsync(
        long id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_writeConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string getDomainSql = @"
SELECT Domain
FROM AllowedObjects
WHERE Id = @Id;";

        var domain = await connection.ExecuteScalarAsync<string?>(
            new CommandDefinition(
                getDomainSql,
                new { Id = id },
                cancellationToken: cancellationToken));

        if (string.IsNullOrWhiteSpace(domain))
        {
            _logger.LogWarning(
                "AllowedObject with Id {Id} was not found for SetIsActiveAsync.",
                id);

            return false;
        }

        const string updateSql = @"
UPDATE AllowedObjects
SET IsActive = @IsActive
WHERE Id = @Id;";

        var rows = await connection.ExecuteAsync(
            new CommandDefinition(
                updateSql,
                new
                {
                    Id = id,
                    IsActive = isActive ? 1 : 0
                },
                cancellationToken: cancellationToken));

        if (rows <= 0)
        {
            return false;
        }

        InvalidateDomainCache(domain);

        _logger.LogInformation(
            "Updated AllowedObject Id {Id} IsActive={IsActive} for domain {Domain}.",
            id,
            isActive,
            domain);

        return true;
    }

    private static string NormalizeDomain(string domain)
    {
        return domain.Trim().ToLowerInvariant();
    }

    private static string BuildCacheKey(string normalizedDomain)
    {
        return CachePrefix + normalizedDomain;
    }

    private void InvalidateDomainCache(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return;
        }

        var normalizedDomain = NormalizeDomain(domain);
        _cache.Remove(BuildCacheKey(normalizedDomain));
    }
}
