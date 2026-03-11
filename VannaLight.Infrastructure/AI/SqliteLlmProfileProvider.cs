using Dapper;
using Microsoft.Data.Sqlite;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;
using VannaLight.Core.Settings;

namespace VannaLight.Infrastructure.AI;

public class SqliteLlmProfileProvider : ILlmRuntimeProfileProvider
{
    private readonly RuntimeDbOptions _options;

    public SqliteLlmProfileProvider(RuntimeDbOptions options)
    {
        _options = options;
    }

    public LlmRuntimeProfile GetActiveProfile()
    {
        using var conn = new SqliteConnection($"Data Source={_options.DbPath};");
        conn.Open();

        const string sql = @"
            SELECT 
                Name, GpuLayerCount, ContextSize, Threads, BatchThreads, 
                BatchSize, UBatchSize, FlashAttention, UseMemorymap, 
                NoKqvOffload, OpOffload
            FROM LlmRuntimeProfile 
            WHERE IsActive = 1 
            LIMIT 1";

        // Intentamos obtener el perfil activo de la base de datos de Runtime
        var profile = conn.QuerySingleOrDefault<LlmRuntimeProfile>(sql);

        // Fallback: Si no hay perfiles en la DB, devolvemos uno seguro para la Quadro T2000
        return profile ?? new LlmRuntimeProfile
        {
            Name = "Fallback-Workstation",
            GpuLayerCount = 15,
            ContextSize = 2048,
            BatchSize = 128,
            UBatchSize = 64
        };
    }
}