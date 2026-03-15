using System;
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
    IBusinessRuleStore businessRuleStore,
    IPatternMatcherService patternMatcher,
    ITemplateSqlBuilder templateSqlBuilder)
{
    private static readonly HashSet<string> AllowedViews = new(StringComparer.OrdinalIgnoreCase)
    {
        "VW_KPIPRODUCTION_V1",
        "VW_KPISCRAP_V1",
        "VW_KPIDOWNTIME_V1"
    };

    public async Task<AskResult> ExecuteAsync(
    string question,
    string sqlitePath,
    string sqlServerConnString,
    CancellationToken ct = default)
    {
        var patternMatch = patternMatcher.Match(question);

        if (patternMatch.IsMatch)
        {
            var patternSql = templateSqlBuilder.BuildSql(patternMatch);

            if (string.IsNullOrWhiteSpace(patternSql))
            {
                return new AskResult(
                    false,
                    string.Empty,
                    "No se pudo construir SQL desde el patrón detectado.",
                    false,
                    AskFailureKind.GenerationError);
            }

            if (!validator.TryValidate(patternSql, out var patternValidationError))
            {
                var reviewId = await reviewStore.EnqueueAsync(
                    sqlitePath,
                    question,
                    patternSql,
                    patternValidationError,
                    "UnsafePattern",
                    ct);

                return new AskResult(
                    false,
                    patternSql,
                    $"Validación fallida en patrón: {patternValidationError}",
                    false,
                    AskFailureKind.ValidationError,
                    reviewId);
            }

            bool passedPatternDryRun = false;

            if (settings.Security.DryRunEnabledByDefault)
            {
                var (ok, error) = await dryRunner.DryRunAsync(sqlServerConnString, patternSql, ct);

                if (!ok)
                {
                    var reviewId = await reviewStore.EnqueueAsync(
                        sqlitePath,
                        question,
                        patternSql,
                        error,
                        "PatternNotCompiling",
                        ct);

                    return new AskResult(
                        false,
                        patternSql,
                        $"Error de compilación en patrón: {error}",
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

        // SOLO si no hubo pattern, caer al flujo LLM
        var context = await retriever.RetrieveAsync(sqlitePath, question, ct);

        var rules = await businessRuleStore.GetActiveRulesAsync(
            sqlitePath,
            "erp-kpi-pilot",
            maxRules: 6,
            ct);

        var prompt = BuildPrompt(question, context, rules);

        var rawSql = await llmClient.GenerateSqlAsync(prompt, ct);
        var cleanSql = CleanLlmOutput(rawSql);

        if (string.IsNullOrWhiteSpace(cleanSql))
        {
            return new AskResult(
                false,
                string.Empty,
                "El modelo no devolvió un SQL utilizable.",
                false,
                AskFailureKind.GenerationError);
        }

        if (!validator.TryValidate(cleanSql, out var validationError))
        {
            var reviewId = await reviewStore.EnqueueAsync(
                sqlitePath,
                question,
                cleanSql,
                validationError,
                "Unsafe",
                ct);

            return new AskResult(
                false,
                cleanSql,
                $"Validación fallida: {validationError}",
                false,
                AskFailureKind.ValidationError,
                reviewId);
        }

        bool passedDryRun = false;

        if (settings.Security.DryRunEnabledByDefault)
        {
            var (ok, error) = await dryRunner.DryRunAsync(sqlServerConnString, cleanSql, ct);

            if (!ok)
            {
                var reviewId = await reviewStore.EnqueueAsync(
                    sqlitePath,
                    question,
                    cleanSql,
                    error,
                    "NotCompiling",
                    ct);

                return new AskResult(
                    false,
                    cleanSql,
                    $"Error de compilación: {error}",
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
                "No se identificó el número de parte para el pronóstico.",
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

    private string BuildPrompt(string question, RetrievalContext context, IReadOnlyList<BusinessRule> rules)
    {
        const int MaxPromptChars = 9000;
        const int MaxRulesChars = 1800;
        const int MaxSchemasChars = 1800;
        const int MaxExamplesChars = 4200;

        var activeRules = (rules ?? Array.Empty<BusinessRule>())
            .Where(r => r != null && !string.IsNullOrWhiteSpace(r.RuleText) && r.IsActive)
            .OrderBy(r => r.Priority)
            .Take(6)
            .Select(r => $"- {r.RuleText.Trim()}")
            .ToList();

        var rulesText = activeRules.Any()
            ? TrimToMax(string.Join("\n", activeRules), MaxRulesChars)
            : string.Empty;

        var schemaLines = (context?.SchemaDocs ?? Enumerable.Empty<RetrievedSchemaDoc>())
            .Where(s => s?.Doc != null)
            .Where(s => AllowedViews.Contains(NormalizeViewName(s.Doc!.Table)))
            .Select(s => s.Doc!.DocText?.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct()
            .Take(3)
            .ToList();

        var schemasText = schemaLines.Any()
            ? TrimToMax(string.Join("\n", schemaLines), MaxSchemasChars)
            : TrimToMax(
                @"Tabla: dbo.vw_KpiProduction_v1. Columnas clave: OperationDate, YearNumber, MonthNumber, YearMonth, WeekOfYear, Shift, ShiftId, PressId, PressName, MoldId, MoldName, PartId, PartNumber, PartName, CustomerId, CustomerName, TargetQty, ProducedQty, ScrapQty, ProductionValue, EfficiencyPct.
Tabla: dbo.vw_KpiScrap_v1. Columnas clave: OperationDate, YearNumber, MonthNumber, YearMonth, WeekOfYear, Shift, ShiftId, PressId, PressName, MoldId, MoldName, PartId, PartNumber, PartName, CustomerId, CustomerName, ScrapReason, ScrapQty, ScrapCost.
Tabla: dbo.vw_KpiDownTime_v1. Columnas clave: OperationDate, YearNumber, MonthNumber, YearMonth, WeekOfYear, Shift, ShiftId, PressId, PressName, MoldId, MoldName, PartId, PartNumber, PartName, CustomerId, CustomerName, DepartmentName, FailureName, DownTimeMinutes, DownTimeHours, IsOpen, DownTimeCost.",
                MaxSchemasChars);

        var exampleLines = (context?.Examples ?? Enumerable.Empty<RetrievedExample>())
            .Where(e => e?.Example != null)
            .Where(e =>
                !string.IsNullOrWhiteSpace(e.Example.Question) &&
                !string.IsNullOrWhiteSpace(e.Example.Sql) &&
                MentionsAllowedView(e.Example.Sql))
            .Take(2)
            .Select(e => $"Pregunta: {e.Example!.Question.Trim()}\nSQL:\n{e.Example.Sql.Trim()}")
            .ToList();

        var examplesText = exampleLines.Any()
            ? TrimToMax(string.Join("\n\n", exampleLines), MaxExamplesChars)
            : string.Empty;

        var sb = new StringBuilder(4096);

        sb.AppendLine("<|im_start|>system");
        sb.AppendLine("Eres un desarrollador experto en T-SQL para SQL Server.");
        sb.AppendLine("Tu tarea es generar SOLO código SQL válido para consultar vistas KPI industriales.");
        sb.AppendLine("Debes basarte estrictamente en las vistas, reglas y ejemplos proporcionados.");
        sb.AppendLine();
        sb.AppendLine("REGLAS CRÍTICAS DE SINTAXIS T-SQL:");
        sb.AppendLine("1. ESTÁ ESTRICTAMENTE PROHIBIDO USAR 'LIMIT'. Para limitar resultados en SQL Server, usa SIEMPRE 'SELECT TOP (N)'.");
        sb.AppendLine("2. Usa EXACTAMENTE los nombres de columnas que aparecen en las vistas relevantes.");
        sb.AppendLine("3. NUNCA compares un valor de texto contra una columna ID.");
        sb.AppendLine("4. Si necesitas cruzar vistas, prefiere joins por IDs y OperationDate.");
        sb.AppendLine("5. Devuelve SOLO el SQL, sin comentarios y sin bloques markdown.");
        sb.AppendLine();
        sb.AppendLine("REGLAS DE TIEMPO E INTERPRETACIÓN:");
        sb.AppendLine("- Hoy: CAST(OperationDate AS date) = CAST(GETDATE() AS date)");
        sb.AppendLine("- Ayer: CAST(OperationDate AS date) = DATEADD(DAY, -1, CAST(GETDATE() AS date))");
        sb.AppendLine("- Mes actual: YearMonth = CONVERT(char(7), GETDATE(), 120)");
        sb.AppendLine("- Semana actual: YearNumber = YEAR(GETDATE()) AND WeekOfYear = DATEPART(ISO_WEEK, GETDATE())");
        sb.AppendLine("- Cuando el usuario diga 'turno actual' o 'del turno', filtra explícitamente por un único ShiftId calculado como el más reciente del día dentro de la vista consultada.");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(rulesText))
        {
            sb.AppendLine("REGLAS DE NEGOCIO IMPORTANTES:");
            sb.AppendLine(rulesText);
            sb.AppendLine();
        }

        sb.AppendLine("VISTAS RELEVANTES:");
        sb.AppendLine(schemasText);
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(examplesText))
        {
            sb.AppendLine("EJEMPLOS RELEVANTES:");
            sb.AppendLine(examplesText);
            sb.AppendLine();
        }

        sb.AppendLine("<|im_end|>");
        sb.AppendLine("<|im_start|>user");
        sb.AppendLine("Pregunta actual:");
        sb.AppendLine(question?.Trim() ?? string.Empty);
        sb.AppendLine("<|im_end|>");
        sb.AppendLine("<|im_start|>assistant");

        return TrimToMax(sb.ToString(), MaxPromptChars);
    }

    private static string NormalizeViewName(string? name)
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

    private static bool MentionsAllowedView(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return false;

        var upperSql = sql.ToUpperInvariant();
        return AllowedViews.Any(v => upperSql.Contains(v, StringComparison.OrdinalIgnoreCase));
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
