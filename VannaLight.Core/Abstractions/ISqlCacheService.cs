using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VannaLight.Core.Models;

namespace VannaLight.Core.Abstractions;

public interface ISqlCacheService
{
    /// <summary>
    /// Busca si existe una consulta previa exitosa para la misma pregunta, usuario y contexto.
    /// </summary>
    Task<(string? Sql, IEnumerable<dynamic>? Data)> TryGetCachedResultAsync(
        string question,
        string userId,
        AskExecutionContext executionContext,
        string sqlServerConnectionString,
        CancellationToken ct);
}
