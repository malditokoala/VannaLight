using VannaLight.Core.Abstractions;
using System.Text.RegularExpressions;

namespace VannaLight.Infrastructure.Security;

using System.Text.RegularExpressions;
using VannaLight.Core.Abstractions;

public class StaticSqlValidator : ISqlValidator
{
    public bool TryValidate(string sql, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(sql))
        {
            error = "La consulta está vacía.";
            return false;
        }

        var normalizedSql = NormalizeSql(sql);
        var upperSql = normalizedSql.ToUpperInvariant();

        // 1) Debe comenzar con SELECT, WITH o DECLARE
        if (!(upperSql.StartsWith("SELECT") ||
              upperSql.StartsWith("WITH") ||
              upperSql.StartsWith("DECLARE")))
        {
            error = "La consulta debe comenzar con SELECT, WITH o DECLARE.";
            return false;
        }

        // 2) Bloquear palabras clave peligrosas
        var dangerousKeywords = new[]
        {
            "INSERT", "UPDATE", "DELETE", "DROP", "TRUNCATE",
            "ALTER", "CREATE", "EXEC", "EXECUTE", "MERGE"
        };

        foreach (var kw in dangerousKeywords)
        {
            if (Regex.IsMatch(upperSql, $@"\b{kw}\b", RegexOptions.CultureInvariant))
            {
                error = $"La consulta contiene una palabra clave no permitida: {kw}";
                return false;
            }
        }

        // 3) Si empieza con DECLARE, debe contener después un SELECT o WITH
        if (upperSql.StartsWith("DECLARE"))
        {
            if (!Regex.IsMatch(upperSql, @"\b(SELECT|WITH)\b", RegexOptions.CultureInvariant))
            {
                error = "Una consulta con DECLARE debe terminar en un SELECT o WITH válido.";
                return false;
            }
        }

        return true;
    }

    private static string NormalizeSql(string sql)
    {
        var cleaned = sql.Trim();

        // Quitar comentarios de línea
        cleaned = Regex.Replace(cleaned, @"--.*?$", string.Empty, RegexOptions.Multiline);

        // Quitar comentarios de bloque
        cleaned = Regex.Replace(cleaned, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        return cleaned.Trim();
    }
}