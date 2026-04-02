using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Extensions.Logging;
using VannaLight.Core.Abstractions;

namespace VannaLight.Infrastructure.Security;

public sealed class StaticSqlValidator : ISqlValidator
{
    private const string DefaultSchemaName = "DBO";

    private static readonly string[] DangerousKeywords =
    {
        "INSERT", "UPDATE", "DELETE", "DROP", "TRUNCATE",
        "ALTER", "CREATE", "EXEC", "EXECUTE", "MERGE",
        "OPENROWSET", "OPENDATASOURCE", "BULK"
    };

    private readonly IAllowedObjectStore _allowedObjectStore;
    private readonly ILogger<StaticSqlValidator> _logger;

    public StaticSqlValidator(
        IAllowedObjectStore allowedObjectStore,
        ILogger<StaticSqlValidator> logger)
    {
        _allowedObjectStore = allowedObjectStore ?? throw new ArgumentNullException(nameof(allowedObjectStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool TryValidate(string sql, string domain, out string error)
    {
        error = string.Empty;

        var effectiveDomain = domain?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(effectiveDomain))
        {
            error = "No hay dominio configurado para validación SQL.";
            return false;
        }

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

        if (!(upperSql.StartsWith("SELECT", StringComparison.Ordinal) ||
              upperSql.StartsWith("WITH", StringComparison.Ordinal)))
        {
            error = "La consulta debe comenzar con SELECT o WITH.";
            return false;
        }

        if (HasUnexpectedSemicolon(normalizedSql))
        {
            error = "No se permiten múltiples statements en una sola consulta.";
            return false;
        }

        foreach (var keyword in DangerousKeywords)
        {
            if (Regex.IsMatch(upperSql, $@"\b{keyword}\b", RegexOptions.CultureInvariant))
            {
                error = $"La consulta contiene una palabra clave no permitida: {keyword}.";
                return false;
            }
        }

        if (Regex.IsMatch(upperSql, @"\bSELECT\b[\s\S]*\bINTO\b", RegexOptions.CultureInvariant))
        {
            error = "No se permite SELECT INTO.";
            return false;
        }

        if (Regex.IsMatch(upperSql, @"(^|[^A-Z0-9_])#\w+", RegexOptions.CultureInvariant))
        {
            error = "No se permiten tablas temporales.";
            return false;
        }

        if (Regex.IsMatch(upperSql, @"\bSYS\.", RegexOptions.CultureInvariant) ||
            Regex.IsMatch(upperSql, @"\bINFORMATION_SCHEMA\.", RegexOptions.CultureInvariant))
        {
            error = "No se permite consultar metadatos del sistema.";
            return false;
        }

        var allowedObjectKeys = LoadAllowedObjectKeys(effectiveDomain);
        if (allowedObjectKeys.Count == 0)
        {
            _logger.LogWarning(
                "Validation blocked because no allowed SQL objects were loaded for domain {Domain}.",
                effectiveDomain);

            error = "No hay objetos permitidos configurados para validar la consulta.";
            return false;
        }

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
            {
                continue;
            }

            if (cteNames.Contains(normalizedObject))
            {
                continue;
            }

            if (!TryBuildAllowedObjectKey(normalizedObject, out var allowedObjectKey))
            {
                error = $"No se pudo interpretar el objeto referenciado: {rawObject}.";
                return false;
            }

            if (!allowedObjectKeys.Contains(allowedObjectKey))
            {
                _logger.LogWarning(
                    "Blocked SQL for domain {Domain}. Referenced object: {Object}.",
                    effectiveDomain,
                    rawObject);

                error = $"La consulta referencia un objeto no permitido: {rawObject}.";
                return false;
            }
        }

        return true;
    }

    private HashSet<string> LoadAllowedObjectKeys(string domain)
    {
        try
        {
            var rows = _allowedObjectStore
                .GetActiveObjectsAsync(domain, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            var allowedKeys = rows
                .Select(x => BuildAllowedObjectKey(x.SchemaName, x.ObjectName))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _logger.LogInformation(
                "Loaded {Count} allowed SQL object keys for domain {Domain}.",
                allowedKeys.Count,
                domain);

            return allowedKeys;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error loading allowed SQL objects for domain {Domain}.",
                domain);

            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
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
        {
            return trimmed[1..].TrimStart();
        }

        return trimmed;
    }

    private static bool HasUnexpectedSemicolon(string sql)
    {
        var trimmed = sql.Trim();

        if (trimmed.EndsWith(";", StringComparison.Ordinal))
        {
            trimmed = trimmed[..^1].TrimEnd();
        }

        return trimmed.Contains(';');
    }

    private static HashSet<string> ExtractCteNames(string upperSql)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!upperSql.StartsWith("WITH", StringComparison.Ordinal))
        {
            return result;
        }

        var matches = Regex.Matches(
            upperSql,
            @"(?:\bWITH\b|,)\s*([A-Z0-9_\[\]]+)\s+AS\s*\(",
            RegexOptions.CultureInvariant);

        foreach (Match match in matches)
        {
            var cteName = NormalizeObjectName(match.Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(cteName))
            {
                result.Add(cteName);
            }
        }

        return result;
    }

    private static bool TryBuildAllowedObjectKey(string normalizedObjectName, out string allowedObjectKey)
    {
        allowedObjectKey = string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedObjectName))
        {
            return false;
        }

        var parts = normalizedObjectName
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 1)
        {
            allowedObjectKey = BuildAllowedObjectKey(DefaultSchemaName, parts[0]);
            return true;
        }

        if (parts.Length >= 2)
        {
            var schemaName = parts[^2];
            var objectName = parts[^1];

            allowedObjectKey = BuildAllowedObjectKey(schemaName, objectName);
            return true;
        }

        return false;
    }

    private static string BuildAllowedObjectKey(string schemaName, string objectName)
    {
        var normalizedSchema = NormalizeObjectName(schemaName);
        var normalizedObject = NormalizeObjectName(objectName);

        if (string.IsNullOrWhiteSpace(normalizedSchema) ||
            string.IsNullOrWhiteSpace(normalizedObject))
        {
            return string.Empty;
        }

        return $"{normalizedSchema}.{normalizedObject}";
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
