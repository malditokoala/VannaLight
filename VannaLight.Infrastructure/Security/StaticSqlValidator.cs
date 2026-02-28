using VannaLight.Core.Abstractions;
using System.Text.RegularExpressions;

namespace VannaLight.Infrastructure.Security;

public class StaticSqlValidator : ISqlValidator
{
    public bool TryValidate(string sql, out string error)
    {
        error = string.Empty;
        var upperSql = sql.ToUpperInvariant();

        // 1. Debe ser un SELECT
        if (!upperSql.TrimStart().StartsWith("SELECT"))
        {
            error = "La consulta debe comenzar con SELECT.";
            return false;
        }

        // 2. Bloquear palabras clave peligrosas (DML y DDL)
        var dangerousKeywords = new[] { "INSERT", "UPDATE", "DELETE", "DROP", "TRUNCATE", "ALTER", "CREATE", "EXEC", "EXECUTE" };
        foreach (var kw in dangerousKeywords)
        {
            // Usamos regex para asegurar que es la palabra completa, no parte de otra palabra
            if (Regex.IsMatch(upperSql, $@"\b{kw}\b"))
            {
                error = $"La consulta contiene una palabra clave no permitida: {kw}";
                return false;
            }
        }

        return true;
    }
}