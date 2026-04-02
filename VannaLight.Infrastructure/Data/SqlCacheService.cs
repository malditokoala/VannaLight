using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Dapper;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;
using VannaLight.Core.Settings;

namespace VannaLight.Infrastructure.Data;

/// <summary>
/// Servicio encargado de gestionar la recuperación de resultados previos desde SQLite
/// y su ejecución en caliente contra el ERP para asegurar datos frescos.
/// </summary>
public class SqlCacheService : ISqlCacheService
{
    private readonly RuntimeDbOptions _runtimeOptions;

    public SqlCacheService(RuntimeDbOptions runtimeOptions)
    {
        _runtimeOptions = runtimeOptions ?? throw new ArgumentNullException(nameof(runtimeOptions));
    }

    /// <summary>
    /// Intenta obtener un resultado cacheado para una pregunta y usuario específicos.
    /// </summary>
    public async Task<(string? Sql, IEnumerable<dynamic>? Data)> TryGetCachedResultAsync(
        string question,
        string userId,
        AskExecutionContext executionContext,
        string sqlServerConnectionString,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(question) || executionContext is null || string.IsNullOrWhiteSpace(sqlServerConnectionString))
            return (null, null);

        // 1. Conexión a la base de datos de estado (SQLite Runtime)
        using var sqliteConn = new SqliteConnection($"Data Source={_runtimeOptions.DbPath};");
        await sqliteConn.OpenAsync(ct);

        const string sqlSearch = @"
            SELECT JobId, SqlText
            FROM QuestionJobs
            WHERE UserId = @userId
              AND Question = @q
              AND TenantKey = @tenantKey
              AND Domain = @domain
              AND ConnectionName = @connectionName
              AND Status = 'Completed'
              AND SqlText IS NOT NULL
            ORDER BY UpdatedUtc DESC
            LIMIT 1";

        var cached = await sqliteConn.QueryFirstOrDefaultAsync<dynamic>(sqlSearch, new
        {
            userId,
            q = question.Trim(),
            tenantKey = executionContext.TenantKey,
            domain = executionContext.Domain,
            connectionName = executionContext.ConnectionName
        });

        if (cached == null || string.IsNullOrEmpty((string)cached.SqlText))
            return (null, null);

        string sqlToExecute = cached.SqlText;

        // 2. Si hay coincidencia, ejecutamos el SQL contra el ERP (SQL Server) para traer datos actualizados
        try
        {
            using var sqlServerConn = new SqlConnection(sqlServerConnectionString);
            var results = await sqlServerConn.QueryAsync(sqlToExecute);
            return (sqlToExecute, results);
        }
        catch (Exception)
        {
            // Si el SQL guardado falla (ej. cambió el esquema del ERP), invalidamos la caché silenciosamente
            return (null, null);
        }
    }
}
