using System.Text.RegularExpressions;
using VannaLight.Core.Abstractions;

namespace VannaLight.Infrastructure.Security;

public class StaticSqlValidator : ISqlValidator
{
    private static readonly HashSet<string> AllowedObjects = new(StringComparer.OrdinalIgnoreCase)
    {
        "DBO.VW_KPIPRODUCTION_V1",
        "DBO.VW_KPISCRAP_V1",
        "DBO.VW_KPIDOWNTIME_V1",
        "VW_KPIPRODUCTION_V1",
        "VW_KPISCRAP_V1",
        "VW_KPIDOWNTIME_V1"
    };

    private static readonly string[] DangerousKeywords =
    {
        "INSERT", "UPDATE", "DELETE", "DROP", "TRUNCATE",
        "ALTER", "CREATE", "EXEC", "EXECUTE", "MERGE",
        "OPENROWSET", "OPENDATASOURCE", "BULK"
    };

    public bool TryValidate(string sql, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(sql))
        {
            error = "La consulta está vacía.";
            return false;
        }

        var normalizedSql = NormalizeSql(sql);
        normalizedSql = NormalizeLeadingWith(normalizedSql);

        if (string.IsNullOrWhiteSpace(normalizedSql))
        {
            error = "La consulta quedó vacía después de limpiar comentarios.";
            return false;
        }

        var upperSql = normalizedSql.ToUpperInvariant();

        // 1) Solo SELECT o WITH para el MVP actual
        if (!(upperSql.StartsWith("SELECT") || upperSql.StartsWith("WITH")))
        {
            error = "La consulta debe comenzar con SELECT o WITH.";
            return false;
        }

        // 2) No permitir múltiples statements
        if (HasUnexpectedSemicolon(normalizedSql))
        {
            error = "No se permiten múltiples statements en una sola consulta.";
            return false;
        }

        // 3) Bloquear palabras clave peligrosas
        foreach (var keyword in DangerousKeywords)
        {
            if (Regex.IsMatch(upperSql, $@"\b{keyword}\b", RegexOptions.CultureInvariant))
            {
                error = $"La consulta contiene una palabra clave no permitida: {keyword}.";
                return false;
            }
        }

        // 4) Bloquear SELECT INTO
        if (Regex.IsMatch(upperSql, @"\bSELECT\b[\s\S]*\bINTO\b", RegexOptions.CultureInvariant))
        {
            error = "No se permite SELECT INTO.";
            return false;
        }

        // 5) Bloquear tablas temporales
        if (Regex.IsMatch(upperSql, @"(^|[^A-Z0-9_])#\w+", RegexOptions.CultureInvariant))
        {
            error = "No se permiten tablas temporales.";
            return false;
        }

        // 6) Bloquear acceso a metadatos del sistema
        if (Regex.IsMatch(upperSql, @"\bSYS\.", RegexOptions.CultureInvariant) ||
            Regex.IsMatch(upperSql, @"\bINFORMATION_SCHEMA\.", RegexOptions.CultureInvariant))
        {
            error = "No se permite consultar metadatos del sistema.";
            return false;
        }

        // 7) Restringir a las vistas permitidas del piloto
        var cteNames = ExtractCteNames(upperSql);

        var objectMatches = Regex.Matches(
            upperSql,
            @"\b(?:FROM|JOIN)\s+([A-Z0-9_\.\[\]#]+)",
            RegexOptions.CultureInvariant);

        foreach (Match match in objectMatches)
        {
            var rawObject = match.Groups[1].Value;
            var normalizedObject = NormalizeObjectName(rawObject);

            if (string.IsNullOrWhiteSpace(normalizedObject))
                continue;

            // Permitir CTEs
            if (cteNames.Contains(normalizedObject))
                continue;

            // Todo objeto referenciado en FROM/JOIN debe estar en whitelist
            if (!AllowedObjects.Contains(normalizedObject))
            {
                error = $"La consulta referencia un objeto no permitido: {rawObject}.";
                return false;
            }
        }

        return true;
    }

    private static string NormalizeSql(string sql)
    {
        var cleaned = sql.Trim();

        cleaned = Regex.Replace(cleaned, @"```sql|```", string.Empty, RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"--.*?$", string.Empty, RegexOptions.Multiline);
        cleaned = Regex.Replace(cleaned, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        return cleaned.Trim();
    }

    private static string NormalizeLeadingWith(string sql)
    {
        var trimmed = sql.Trim();

        if (trimmed.StartsWith(";WITH", StringComparison.OrdinalIgnoreCase))
            return trimmed[1..].TrimStart();

        return trimmed;
    }

    private static bool HasUnexpectedSemicolon(string sql)
    {
        var trimmed = sql.Trim();

        // Permitimos ; final opcional
        if (trimmed.EndsWith(";"))
            trimmed = trimmed[..^1].TrimEnd();

        return trimmed.Contains(';');
    }

    private static HashSet<string> ExtractCteNames(string upperSql)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!upperSql.StartsWith("WITH"))
            return result;

        var matches = Regex.Matches(
            upperSql,
            @"(?:\bWITH\b|,)\s*([A-Z0-9_\[\]]+)\s+AS\s*\(",
            RegexOptions.CultureInvariant);

        foreach (Match match in matches)
        {
            var cteName = NormalizeObjectName(match.Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(cteName))
                result.Add(cteName);
        }

        return result;
    }

    private static string NormalizeObjectName(string objectName)
    {
        return objectName
            .Replace("[", string.Empty)
            .Replace("]", string.Empty)
            .Trim()
            .ToUpperInvariant();
    }
}
