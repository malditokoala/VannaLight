using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;
using VannaLight.Core.Settings;

namespace VannaLight.Core.UseCases;

public enum AskFailureKind
{
    None = 0,
    GenerationError = 1,
    ValidationError = 2,
    DryRunError = 3
}

public record AskResult(
    bool Success,
    string Sql,
    string? Error,
    bool PassedDryRun,
    AskFailureKind FailureKind = AskFailureKind.None,
    long? ReviewId = null,
    string? ResultJson = null);

public class AskUseCase(
    IRetriever retriever,
    ILlmClient llmClient,
    ISqlValidator validator,
    ISqlDryRunner dryRunner,
    IReviewStore reviewStore,
    AppSettings settings,
    IPredictionIntentRouter predictionRouter,
    IForecastingService forecaster,
    IPredictionAnswerService humanizer,
    ISystemConfigProvider systemConfigProvider,
    IBusinessRuleStore businessRuleStore,
    ISemanticHintStore semanticHintStore,
    IPatternMatcherService patternMatcher,
    ITemplateSqlBuilder templateSqlBuilder,
    IAllowedObjectStore allowedObjectStore)
{
    public async Task<AskResult> ExecuteAsync(
        string question,
        string memoryDbPath,
        string runtimeDbPath,
        string sqlServerConnString,
        string domain,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return new AskResult(
                false,
                string.Empty,
                "La pregunta esta vacia.",
                false,
                AskFailureKind.GenerationError);
        }

        if (string.IsNullOrWhiteSpace(memoryDbPath))
        {
            return new AskResult(
                false,
                string.Empty,
                "No se configuro la base de memoria.",
                false,
                AskFailureKind.GenerationError);
        }

        if (string.IsNullOrWhiteSpace(runtimeDbPath))
        {
            return new AskResult(
                false,
                string.Empty,
                "No se configuro la base de runtime.",
                false,
                AskFailureKind.GenerationError);
        }

        if (string.IsNullOrWhiteSpace(domain))
        {
            return new AskResult(
                false,
                string.Empty,
                "No hay dominio configurado para recuperacion de reglas.",
                false,
                AskFailureKind.GenerationError);
        }

        var normalizedDomain = domain.Trim();

        var allowedObjectNames = await GetAllowedObjectNamesAsync(
                normalizedDomain,
                ct);

        if (allowedObjectNames.Count == 0)
        {
            return new AskResult(
                false,
                string.Empty,
                $"No hay objetos SQL permitidos configurados para el dominio '{normalizedDomain}'.",
                false,
                AskFailureKind.GenerationError);
        }

        var patternMatch = await patternMatcher.MatchAsync(question, normalizedDomain, ct);
        var inferredIntentName = string.IsNullOrWhiteSpace(patternMatch.IntentName)
            ? null
            : patternMatch.IntentName;

        if (patternMatch.IsMatch)
        {
            var patternSql = templateSqlBuilder.BuildSql(patternMatch);

            if (string.IsNullOrWhiteSpace(patternSql))
            {
                return new AskResult(
                    false,
                    string.Empty,
                    "No se pudo construir SQL desde el patron detectado.",
                    false,
                    AskFailureKind.GenerationError);
            }

            if (!validator.TryValidate(patternSql, out var patternValidationError))
            {
                var reviewId = await TryEnqueueReviewAsync(
                    runtimeDbPath,
                    question,
                    patternSql,
                    patternValidationError,
                    "UnsafePattern",
                    ct);

                return new AskResult(
                    false,
                    patternSql,
                    $"Validacion fallida en patron: {patternValidationError}",
                    false,
                    AskFailureKind.ValidationError,
                    reviewId);
            }

            var passedPatternDryRun = false;

            if (settings.Security.DryRunEnabledByDefault)
            {
                var (ok, error) = await dryRunner.DryRunAsync(sqlServerConnString, patternSql, ct);

                if (!ok)
                {
                    var reviewId = await TryEnqueueReviewAsync(
                        runtimeDbPath,
                        question,
                        patternSql,
                        error,
                        "PatternNotCompiling",
                        ct);

                    return new AskResult(
                        false,
                        patternSql,
                        $"Error de compilacion en patron: {error}",
                        false,
                        AskFailureKind.DryRunError,
                        reviewId);
                }

                passedPatternDryRun = true;
            }

            return new AskResult(
                true,
                patternSql,
                null,
                passedPatternDryRun,
                AskFailureKind.None);
        }

        // Solo si no hubo pattern, caer al flujo LLM
        var context = await retriever.RetrieveAsync(
            memoryDbPath,
            question,
            normalizedDomain,
            inferredIntentName,
            ct);

        var rules = await businessRuleStore.GetActiveRulesAsync(
            memoryDbPath,
            normalizedDomain,
            maxRules: 6,
            ct);

        var semanticHints = await semanticHintStore.GetActiveHintsAsync(
            memoryDbPath,
            normalizedDomain,
            maxHints: 8,
            ct);

        var prompt = await BuildPromptAsync(question, context, rules, semanticHints, allowedObjectNames, ct);

        var rawSql = await llmClient.GenerateSqlAsync(prompt, ct);
        var cleanSql = CleanLlmOutput(rawSql);

        if (string.IsNullOrWhiteSpace(cleanSql))
        {
            return new AskResult(
                false,
                string.Empty,
                "El modelo no devolvio un SQL utilizable.",
                false,
                AskFailureKind.GenerationError);
        }

        if (!validator.TryValidate(cleanSql, out var validationError))
        {
            var reviewId = await TryEnqueueReviewAsync(
                runtimeDbPath,
                question,
                cleanSql,
                validationError,
                "Unsafe",
                ct);

            return new AskResult(
                false,
                cleanSql,
                $"Validacion fallida: {validationError}",
                false,
                AskFailureKind.ValidationError,
                reviewId);
        }

        var passedDryRun = false;

        if (settings.Security.DryRunEnabledByDefault)
        {
            var (ok, error) = await dryRunner.DryRunAsync(sqlServerConnString, cleanSql, ct);

            if (!ok)
            {
                var reviewId = await TryEnqueueReviewAsync(
                    runtimeDbPath,
                    question,
                    cleanSql,
                    error,
                    "NotCompiling",
                    ct);

                return new AskResult(
                    false,
                    cleanSql,
                    $"Error de compilacion: {error}",
                    false,
                    AskFailureKind.DryRunError,
                    reviewId);
            }

            passedDryRun = true;
        }

        return new AskResult(
            true,
            cleanSql,
            null,
            passedDryRun,
            AskFailureKind.None);
    }

    public async Task<AskResult> PredictAsync(string question, CancellationToken ct = default)
    {
        var intent = await predictionRouter.ParseAsync(question, ct);

        if (!intent.IsPredictionRequest || string.IsNullOrWhiteSpace(intent.EntityName))
        {
            return new AskResult(
                false,
                string.Empty,
                "No se identifico el numero de parte para el pronostico.",
                false,
                AskFailureKind.GenerationError);
        }

        intent = await forecaster.PredictAsync(intent);
        var humanizedText = await humanizer.HumanizeAsync(intent, ct);

        var predictionJson = JsonSerializer.Serialize(new
        {
            type = "prediction",
            explanation = humanizedText,
            data = new
            {
                EntityName = intent.EntityName,
                PredictedValue = intent.PredictedValue,
                HistoryShiftsUsed = intent.HistoryShiftsUsed,
                ForecastPeriodLabel = intent.ForecastPeriodLabel
            }
        });

        return new AskResult(
            true,
            humanizedText,
            null,
            true,
            AskFailureKind.None,
            null,
            predictionJson);
    }

    private async Task<IReadOnlyList<string>> GetAllowedObjectNamesAsync(
    string domain,
    CancellationToken ct)
    {
        var allowedObjects = await allowedObjectStore.GetActiveObjectsAsync(
            domain,
            ct);

        return allowedObjects
            .Where(x => x != null && !string.IsNullOrWhiteSpace(x.ObjectName))
            .Select(x =>
                string.IsNullOrWhiteSpace(x.SchemaName)
                    ? x.ObjectName.Trim()
                    : $"{x.SchemaName.Trim()}.{x.ObjectName.Trim()}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<long?> TryEnqueueReviewAsync(
        string runtimeDbPath,
        string question,
        string generatedSql,
        string? errorMessage,
        string reason,
        CancellationToken ct)
    {
        try
        {
            return await reviewStore.EnqueueAsync(
                runtimeDbPath,
                question,
                generatedSql,
                errorMessage,
                reason,
                ct);
        }
        catch
        {
            return null;
        }
    }

    private async Task<string> BuildPromptAsync(
        string question,
        RetrievalContext context,
        IReadOnlyList<BusinessRule> rules,
        IReadOnlyList<SemanticHint> semanticHints,
        IReadOnlyList<string> allowedObjectNames,
        CancellationToken ct)
    {
        var maxPromptChars = await GetPromptIntAsync("MaxPromptChars", 9000, ct);
        var maxRulesChars = await GetPromptIntAsync("MaxRulesChars", 1800, ct);
        var maxSemanticHintsChars = await GetPromptIntAsync("MaxSemanticHintsChars", 1400, ct);
        var maxSchemasChars = await GetPromptIntAsync("MaxSchemasChars", 1800, ct);
        var maxExamplesChars = await GetPromptIntAsync("MaxExamplesChars", 4200, ct);
        var maxRules = await GetPromptIntAsync("MaxRules", 6, ct);
        var maxSemanticHints = await GetPromptIntAsync("MaxSemanticHints", 8, ct);
        var maxSchemas = await GetPromptIntAsync("MaxSchemas", 3, ct);
        var maxExamples = await GetPromptIntAsync("MaxExamples", 2, ct);
        var systemPersona = await GetPromptTextAsync("SystemPersona", "Eres un desarrollador experto en T-SQL para SQL Server.", ct);
        var taskInstruction = await GetPromptTextAsync("TaskInstruction", "Tu tarea es generar SOLO codigo SQL valido para SQL Server.", ct);
        var contextInstruction = await GetPromptTextAsync("ContextInstruction", "Debes basarte estrictamente en los objetos SQL permitidos, reglas, esquemas y ejemplos proporcionados.", ct);
        var sqlSyntaxRules = await GetPromptTextAsync(
            "SqlSyntaxRules",
            """
            1. ESTA ESTRICTAMENTE PROHIBIDO USAR 'LIMIT'. Para limitar resultados en SQL Server, usa SIEMPRE 'SELECT TOP (N)'.
            2. Usa EXACTAMENTE los nombres de columnas que aparezcan en los esquemas recuperados y ejemplos validos.
            3. NUNCA compares un valor de texto contra una columna ID.
            4. Si necesitas cruzar objetos SQL permitidos, prefiere joins por IDs y OperationDate.
            5. Devuelve SOLO el SQL, sin comentarios y sin bloques markdown.
            """,
            ct);
        var timeInterpretationRules = await GetPromptTextAsync(
            "TimeInterpretationRules",
            """
            - Hoy: CAST(OperationDate AS date) = CAST(GETDATE() AS date)
            - Ayer: CAST(OperationDate AS date) = DATEADD(DAY, -1, CAST(GETDATE() AS date))
            - Mes actual: YearMonth = CONVERT(char(7), GETDATE(), 120)
            - Semana actual: YearNumber = YEAR(GETDATE()) AND WeekOfYear = DATEPART(ISO_WEEK, GETDATE())
            - Cuando el usuario diga 'turno actual' o 'del turno', filtra explicitamente por un unico ShiftId calculado como el mas reciente del dia dentro de la vista consultada.
            """,
            ct);
        var rulesHeader = await GetPromptTextAsync("BusinessRulesHeader", "REGLAS DE NEGOCIO IMPORTANTES:", ct);
        var semanticHintsHeader = await GetPromptTextAsync("SemanticHintsHeader", "PISTAS SEMANTICAS DEL DOMINIO:", ct);
        var allowedObjectsHeader = await GetPromptTextAsync("AllowedObjectsHeader", "OBJETOS SQL PERMITIDOS:", ct);
        var schemasHeader = await GetPromptTextAsync("SchemasHeader", "ESQUEMAS RELEVANTES RECUPERADOS:", ct);
        var examplesHeader = await GetPromptTextAsync("ExamplesHeader", "EJEMPLOS RELEVANTES:", ct);
        var questionHeader = await GetPromptTextAsync("QuestionHeader", "Pregunta actual:", ct);

        var normalizedAllowedObjects = allowedObjectNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(NormalizeObjectName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var allowedObjectsText = string.Join(
            "\n",
            allowedObjectNames.Select(x => $"- {x}"));

        var activeRules = (rules ?? Array.Empty<BusinessRule>())
            .Where(r => r != null && !string.IsNullOrWhiteSpace(r.RuleText) && r.IsActive)
            .OrderBy(r => r.Priority)
            .Take(maxRules)
            .Select(r => $"- {r.RuleText.Trim()}")
            .ToList();

        var rulesText = activeRules.Any()
            ? TrimToMax(string.Join("\n", activeRules), maxRulesChars)
            : string.Empty;

        var semanticHintLines = (semanticHints ?? Array.Empty<SemanticHint>())
            .Where(h => h != null && h.IsActive && !string.IsNullOrWhiteSpace(h.HintText))
            .Where(h => ShouldIncludeSemanticHint(h, normalizedAllowedObjects))
            .OrderBy(h => h.Priority)
            .Take(maxSemanticHints)
            .Select(FormatSemanticHint)
            .ToList();

        var semanticHintsText = semanticHintLines.Any()
            ? TrimToMax(string.Join("\n", semanticHintLines), maxSemanticHintsChars)
            : string.Empty;

        var schemaLines = (context?.SchemaDocs ?? Enumerable.Empty<RetrievedSchemaDoc>())
            .Where(s => s?.Doc != null)
            .Where(s => normalizedAllowedObjects.Contains(NormalizeObjectName(s.Doc!.Table)))
            .Select(s => s.Doc!.DocText?.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct()
            .Take(maxSchemas)
            .ToList();

        var schemasText = schemaLines.Any()
            ? TrimToMax(string.Join("\n", schemaLines), maxSchemasChars)
            : string.Empty;

        var exampleLines = (context?.Examples ?? Enumerable.Empty<RetrievedExample>())
            .Where(e => e?.Example != null)
            .Where(e =>
                !string.IsNullOrWhiteSpace(e.Example.Question) &&
                !string.IsNullOrWhiteSpace(e.Example.Sql) &&
                MentionsAllowedObject(e.Example.Sql, normalizedAllowedObjects))
            .Take(maxExamples)
            .Select(e => $"Pregunta: {e.Example!.Question.Trim()}\nSQL:\n{e.Example.Sql.Trim()}")
            .ToList();

        var examplesText = exampleLines.Any()
            ? TrimToMax(string.Join("\n\n", exampleLines), maxExamplesChars)
            : string.Empty;

        var sb = new StringBuilder(4096);

        sb.AppendLine("<|im_start|>system");
        sb.AppendLine(systemPersona);
        sb.AppendLine(taskInstruction);
        sb.AppendLine(contextInstruction);
        sb.AppendLine();
        sb.AppendLine("REGLAS CRITICAS DE SINTAXIS T-SQL:");
        sb.AppendLine(sqlSyntaxRules);
        sb.AppendLine();
        sb.AppendLine("REGLAS DE TIEMPO E INTERPRETACION:");
        sb.AppendLine(timeInterpretationRules);
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(rulesText))
        {
            sb.AppendLine(rulesHeader);
            sb.AppendLine(rulesText);
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(semanticHintsText))
        {
            sb.AppendLine(semanticHintsHeader);
            sb.AppendLine(semanticHintsText);
            sb.AppendLine();
        }

        sb.AppendLine(allowedObjectsHeader);
        sb.AppendLine(allowedObjectsText);
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(schemasText))
        {
            sb.AppendLine(schemasHeader);
            sb.AppendLine(schemasText);
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(examplesText))
        {
            sb.AppendLine(examplesHeader);
            sb.AppendLine(examplesText);
            sb.AppendLine();
        }

        sb.AppendLine("<|im_end|>");
        sb.AppendLine("<|im_start|>user");
        sb.AppendLine(questionHeader);
        sb.AppendLine(question?.Trim() ?? string.Empty);
        sb.AppendLine("<|im_end|>");
        sb.AppendLine("<|im_start|>assistant");

        return TrimToMax(sb.ToString(), maxPromptChars);
    }

    private async Task<int> GetPromptIntAsync(string key, int fallback, CancellationToken ct)
    {
        var value = await systemConfigProvider.GetIntAsync("Prompting", key, ct);
        return value.HasValue && value.Value > 0 ? value.Value : fallback;
    }

    private async Task<string> GetPromptTextAsync(string key, string fallback, CancellationToken ct)
    {
        var value = await systemConfigProvider.GetValueAsync("Prompting", key, ct);
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string NormalizeObjectName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var cleaned = name.Trim()
            .Replace("[", string.Empty)
            .Replace("]", string.Empty);

        if (cleaned.StartsWith("dbo.", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned[4..];

        return cleaned.ToUpperInvariant();
    }

    private static bool MentionsAllowedObject(
        string sql,
        IReadOnlyCollection<string> normalizedAllowedObjects)
    {
        if (string.IsNullOrWhiteSpace(sql) || normalizedAllowedObjects.Count == 0)
            return false;

        var normalizedSql = sql.ToUpperInvariant()
            .Replace("[", string.Empty)
            .Replace("]", string.Empty);

        return normalizedAllowedObjects.Any(obj =>
            normalizedSql.Contains(obj, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ShouldIncludeSemanticHint(
        SemanticHint hint,
        IReadOnlySet<string> normalizedAllowedObjects)
    {
        if (hint is null)
            return false;

        if (string.IsNullOrWhiteSpace(hint.ObjectName))
            return true;

        return normalizedAllowedObjects.Contains(NormalizeObjectName(hint.ObjectName));
    }

    private static string FormatSemanticHint(SemanticHint hint)
    {
        var label = !string.IsNullOrWhiteSpace(hint.DisplayName)
            ? hint.DisplayName!.Trim()
            : hint.HintKey.Trim();

        var targetParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(hint.ObjectName))
            targetParts.Add(hint.ObjectName.Trim());
        if (!string.IsNullOrWhiteSpace(hint.ColumnName))
            targetParts.Add(hint.ColumnName.Trim());

        var target = targetParts.Count > 0
            ? $" | Target: {string.Join(".", targetParts)}"
            : string.Empty;

        var hintType = string.IsNullOrWhiteSpace(hint.HintType)
            ? "hint"
            : hint.HintType.Trim().ToLowerInvariant();

        return $"- [{hintType}] {label}: {hint.HintText.Trim()}{target}";
    }

    private static string TrimToMax(string text, int maxChars)
    {
        const string suffix = "\n...[contenido truncado por presupuesto de prompt]";

        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        if (text.Length <= maxChars)
            return text;

        if (maxChars <= suffix.Length)
            return text.Substring(0, maxChars);

        return text.Substring(0, maxChars - suffix.Length) + suffix;
    }

    private static string CleanLlmOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return string.Empty;

        var clean = output.Trim();

        clean = Regex.Replace(clean, @"```sql|```", string.Empty, RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"<\|im_end\|>.*$", string.Empty, RegexOptions.Singleline);

        var sqlMatch = Regex.Match(clean, @"(?is)\b(WITH|SELECT)\b[\s\S]*");
        if (sqlMatch.Success)
            clean = sqlMatch.Value;

        return clean.Trim();
    }
}
