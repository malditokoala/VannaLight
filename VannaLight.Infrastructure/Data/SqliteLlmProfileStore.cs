using Dapper;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;
using VannaLight.Core.Settings;

namespace VannaLight.Infrastructure.Data;

public class SqliteLlmProfileStore : ILlmProfileStore
{
    private readonly string _connString;

    public SqliteLlmProfileStore(RuntimeDbOptions options)
    {
        _connString = $"Data Source={options.DbPath}";
    }

    public async Task<IEnumerable<LlmRuntimeProfile>> GetAllAsync(CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connString);
        return await conn.QueryAsync<LlmRuntimeProfile>("SELECT * FROM LlmRuntimeProfile ORDER BY Id");
    }

    public async Task<bool> ActivateAsync(int id, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connString);
        await conn.OpenAsync(ct);
        using var tx = conn.BeginTransaction();

        // 1. Apagar todos
        await conn.ExecuteAsync("UPDATE LlmRuntimeProfile SET IsActive = 0", transaction: tx);

        // 2. Encender el elegido
        var updated = await conn.ExecuteAsync(@"
            UPDATE LlmRuntimeProfile 
            SET IsActive = 1, UpdatedUtc = DATETIME('now') 
            WHERE Id = @Id", new { Id = id }, transaction: tx);

        if (updated == 0) return false;

        tx.Commit();
        return true;
    }

    public async Task<bool> UpdateAsync(int id, int? gpuLayers, uint? contextSize, int? batchSize, int? uBatchSize, int? threads, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connString);
        var sql = @"
            UPDATE LlmRuntimeProfile
            SET GpuLayerCount = COALESCE(@GpuLayers, GpuLayerCount),
                ContextSize = COALESCE(@ContextSize, ContextSize),
                BatchSize = COALESCE(@BatchSize, BatchSize),
                UBatchSize = COALESCE(@UBatchSize, UBatchSize),
                Threads = COALESCE(@Threads, Threads),
                UpdatedUtc = DATETIME('now')
            WHERE Id = @Id";

        var rows = await conn.ExecuteAsync(sql, new { id, GpuLayers = gpuLayers, ContextSize = contextSize, BatchSize = batchSize, UBatchSize = uBatchSize, Threads = threads });
        return rows > 0;
    }
}