using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;
using VannaLight.Core.Settings;

namespace VannaLight.Core.UseCases;

public record AskResult(bool Success, string Sql, string? Error, bool PassedDryRun, long? ReviewId = null, string? ResultJson = null);

public class AskUseCase(
    IRetriever retriever,
    ILlmClient llmClient,
    ISqlValidator validator,
    ISqlDryRunner dryRunner,
    IReviewStore reviewStore,
    AppSettings settings,
    IPredictionIntentRouter predictionRouter,
    IForecastingService forecaster,
    IPredictionAnswerService humanizer)
{
    // ==========================================
    // MODO DATOS (SQL) - Conectado al ERP Real / Piloto
    // ==========================================
    public async Task<AskResult> ExecuteAsync(string question, string sqlitePath, string sqlServerConnString, CancellationToken ct = default)
    {
        var context = await retriever.RetrieveAsync(sqlitePath, question, ct);
        var prompt = BuildPrompt(question, context);

        var rawSql = await llmClient.GenerateSqlAsync(prompt, ct);
        var cleanSql = CleanLlmOutput(rawSql);

        // Validación estática
        if (!validator.TryValidate(cleanSql, out var validationError))
        {
            var reviewId = await reviewStore.EnqueueAsync(sqlitePath, question, cleanSql, validationError, "Unsafe", ct);
            return new AskResult(false, cleanSql, $"Validación fallida: {validationError}", false, reviewId);
        }

        // Validación dinámica (Dry-Run en Producción/Piloto local)
        bool passedDryRun = false;
        if (settings.Security.DryRunEnabledByDefault)
        {
            var (ok, error) = await dryRunner.DryRunAsync(sqlServerConnString, cleanSql, ct);
            if (!ok)
            {
                var reviewId = await reviewStore.EnqueueAsync(sqlitePath, question, cleanSql, error, "NotCompiling", ct);
                return new AskResult(false, cleanSql, $"Error de compilación en SQL Server: {error}", false, reviewId);
            }
            passedDryRun = true;
        }

        return new AskResult(true, cleanSql, null, passedDryRun);
    }

    // ==========================================
    // MODO PREDICCIONES (ML.NET)
    // ==========================================
    public async Task<AskResult> PredictAsync(string question, CancellationToken ct = default)
    {
        var intent = await predictionRouter.ParseAsync(question, ct);

        if (!intent.IsPredictionRequest || string.IsNullOrEmpty(intent.EntityName))
            return new AskResult(false, "", "No detecté de qué producto o máquina quieres el pronóstico. Por favor especifica el nombre.", false);

        intent = await forecaster.PredictAsync(intent);
        var humanizedText = await humanizer.HumanizeAsync(intent, ct);

        var predictionJson = JsonSerializer.Serialize(new
        {
            type = "prediction",
            explanation = humanizedText,
            data = new
            {
                ProductName = intent.EntityName,
                PredictedSales = intent.PredictedValue,
                Confidence = intent.ConfidenceScore
            }
        });

        return new AskResult(true, humanizedText, null, true, null, predictionJson);
    }

    // ==========================================
    // HELPERS (El Prompt de Producción/ERP)
    // ==========================================
    private string BuildPrompt(string question, RetrievalContext context)
    {
        var prompt = @"<|im_start|>system
        Eres un desarrollador Senior experto en T-SQL para SQL Server en una planta de manufactura.
        Tu única tarea es devolver código T-SQL válido para SQL Server.
        NO des explicaciones.
        NO uses markdown.
        NO pongas texto adicional.
        Devuelve SOLO el SQL.

        REGLAS GENERALES
        1. SOLO puedes usar estas 3 vistas:
           - dbo.vw_KpiProduction_v1
           - dbo.vw_KpiScrap_v1
           - dbo.vw_KpiDownTime_v1
        2. NO inventes tablas, vistas ni columnas.
        3. Usa nombres de columnas exactamente como existen.
        4. Genera SQL compatible con SQL Server.
        5. Si el usuario pide un número explícito, usa TOP (N).
        6. Si pide 'el que más', usa TOP (1).
        7. Si pide 'los que más' sin número, usa TOP (10).
        8. Usa alias legibles.
        9. Prefiere consultas simples, correctas y seguras.

        MODELO DE DATOS

        dbo.vw_KpiProduction_v1
        - Grano: 1 fila = 1 registro de producción
        - Columnas clave: OperationDate, YearNumber, MonthNumber, YearMonth, WeekOfYear, Shift, ShiftId, PressId, PressName, MoldId, MoldName, PartId, PartNumber, PartName, CustomerId, CustomerName, TargetQty, ProducedQty, ScrapQty, UnitCost, ProductionValue, ScrapValue, EfficiencyPct

        dbo.vw_KpiScrap_v1
        - Grano: 1 fila = 1 evento de scrap
        - Columnas clave: OperationDate, YearNumber, MonthNumber, YearMonth, WeekOfYear, Shift, ShiftId, PressId, PressName, MoldId, MoldName, PartId, PartNumber, PartName, CustomerId, CustomerName, ScrapReasonId, ScrapReasonCode, ScrapReason, ScrapQty, UnitCost, ScrapCost

        dbo.vw_KpiDownTime_v1
        - Grano: 1 fila = 1 evento de tiempo caído
        - Columnas clave: OperationDate, YearNumber, MonthNumber, YearMonth, WeekOfYear, Shift, ShiftId, PressId, PressName, MoldId, MoldName, PartId, PartNumber, PartName, CustomerId, CustomerName, DepartmentId, DepartmentName, FailureId, FailureName, DownTimeMinutes, DownTimeHours, IsOpen, DownTimeCost, LostPieces

        REGLAS DE NEGOCIO
        1. En producción:
           - TargetQty = meta del día
           - ProducedQty = producción real del día
           - ScrapQty = scrap del día
           - EfficiencyPct = ProducedQty / TargetQty * 100
        2. En downtime:
           - DownTimeMinutes y DownTimeHours representan tiempo caído
           - IsOpen = 1 indica evento abierto
           - DownTimeCost es costo estimado
        3. En scrap:
           - ScrapCost es costo estimado del scrap

        REGLAS DE TIEMPO
        1. Usa OperationDate como fecha principal.
        2. Para 'hoy': CAST(OperationDate AS date) = CAST(GETDATE() AS date)
        3. Para 'ayer': CAST(OperationDate AS date) = DATEADD(DAY, -1, CAST(GETDATE() AS date))
        4. Para 'semana actual': YearNumber = YEAR(GETDATE()) AND WeekOfYear = DATEPART(ISO_WEEK, GETDATE())
        5. Para 'mes actual': YearMonth = CONVERT(char(7), GETDATE(), 120)

        REGLAS DE JOIN
        1. Si necesitas cruzar vistas, usa preferentemente:
           - PressId
           - MoldId
           - PartId
           - CustomerId
           - OperationDate
        2. NO uses nombres para joins si existe Id.
        3. Para cruces diarios por prensa, usa PressId + OperationDate.

        REGLAS SOBRE EJEMPLOS RECUPERADOS
        1. Si se incluyen ejemplos de consultas correctas, úsalos como referencia prioritaria de estructura, estilo y semántica.
        2. Reutiliza patrones de SQL de los ejemplos si aplican a la nueva pregunta.
        3. No copies un ejemplo si no corresponde a la intención actual.

        SALIDA
        1. Devuelve SOLO T-SQL.
        2. No uses ```sql.
        3. No agregues comentarios.
        4. No expliques nada.
        ";

        if (context.Examples.Any())
        {
            prompt += "\n<|im_end|>\n<|im_start|>user\n";
            prompt += "EJEMPLOS DE CONSULTAS CORRECTAS Y RELEVANTES:\n\n";

            foreach (var ex in context.Examples)
            {
                prompt += $"Pregunta: {ex.Example.Question}\n";
                prompt += $"SQL:\n{ex.Example.Sql}\n\n";
            }

            prompt += "<|im_end|>\n<|im_start|>user\n";
        }
        else
        {
            prompt += "<|im_end|>\n<|im_start|>user\n";
        }

        prompt += $"Genera el T-SQL para esta solicitud:\n{question}\n<|im_end|>\n<|im_start|>assistant\n";
        return prompt;
    }

    private string CleanLlmOutput(string output)
    {
        var clean = output.Trim();
        if (clean.StartsWith("```sql")) clean = clean.Substring(6);
        if (clean.StartsWith("```")) clean = clean.Substring(3);
        if (clean.EndsWith("```")) clean = clean.Substring(0, clean.Length - 3);
        return clean.Trim();
    }
}