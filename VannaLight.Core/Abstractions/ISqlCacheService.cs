using System;
using System.Collections.Generic;
using System.Text;

namespace VannaLight.Core.Abstractions;

public interface ISqlCacheService
{
    /// <summary>
    /// Busca si existe una consulta previa exitosa para la misma pregunta y usuario.
    /// </summary>
    Task<(string? Sql, IEnumerable<dynamic>? Data)> TryGetCachedResultAsync(string question, string userId, CancellationToken ct);
}
