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
    private readonly string _connectionString;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SqliteAllowedObjectStore> _logger;

    public SqliteAllowedObjectStore(
        IOptions<SqliteOptions> sqliteOptions,
        IMemoryCache cache,
        ILogger<SqliteAllowedObjectStore> logger)
    {
        if (sqliteOptions == null) throw new ArgumentNullException(nameof(sqliteOptions));
        if (sqliteOptions.Value == null) throw new ArgumentNullException(nameof(sqliteOptions.Value));

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

        _connectionString = $"Data Source={dbPath};Mode=ReadOnly;Cache=Shared;";
    }

    public async Task<IReadOnlyList<AllowedObject>> GetActiveObjectsAsync(
        string domain,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            throw new ArgumentException("Domain es requerido.", nameof(domain));
        }

        var normalizedDomain = domain.Trim().ToLowerInvariant();
        var cacheKey = CachePrefix + normalizedDomain;

        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<AllowedObject>? cached) && cached != null)
        {
            return cached;
        }

        try
        {
            await using var connection = new SqliteConnection(_connectionString);
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
                "Loaded {Count} allowed objects for domain {Domain}.",
                rows.Count,
                normalizedDomain);

            return rows;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error loading AllowedObjects for domain {Domain}.",
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
}