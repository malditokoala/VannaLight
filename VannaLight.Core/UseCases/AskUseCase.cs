using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;
using VannaLight.Core.Settings;

namespace VannaLight.Core.UseCases;

// 1. Ampliamos AskResult para enviar el ResultJson a la Interfaz Web
public record AskResult(bool Success, string Sql, string? Error, bool PassedDryRun, long? ReviewId = null, string? ResultJson = null);

// 2. Inyectamos los servicios de ML.NET (Router, Forecaster, Humanizer)
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
    // MODO DATOS (SQL) - Tu flujo original intacto
    // ==========================================
    public async Task<AskResult> ExecuteAsync(string question, string sqlitePath, string sqlServerConnString, CancellationToken ct = default)
    {
        var context = await retriever.RetrieveAsync(sqlitePath, question, ct);
        var prompt = BuildPrompt(question, context);

        var rawSql = await llmClient.GenerateSqlAsync(prompt, ct);
        var cleanSql = CleanLlmOutput(rawSql);

        if (!validator.TryValidate(cleanSql, out var validationError))
        {
            var reviewId = await reviewStore.EnqueueAsync(sqlitePath, question, cleanSql, validationError, ReviewReason.Unsafe.ToString(), ct);
            return new AskResult(false, cleanSql, $"Validación fallida: {validationError}", false, reviewId);
        }

        bool passedDryRun = false;
        if (settings.Security.DryRunEnabledByDefault)
        {
            var (ok, error) = await dryRunner.DryRunAsync(sqlServerConnString, cleanSql, ct);
            if (!ok)
            {
                var reviewId = await reviewStore.EnqueueAsync(sqlitePath, question, cleanSql, error, ReviewReason.NotCompiling.ToString(), ct);
                return new AskResult(false, cleanSql, $"Error de compilación en SQL Server: {error}", false, reviewId);
            }
            passedDryRun = true;
        }

        return new AskResult(true, cleanSql, null, passedDryRun);
    }

    // ==========================================
    // MODO PREDICCIONES (ML.NET) - NUEVO
    // ==========================================
    public async Task<AskResult> PredictAsync(string question, CancellationToken ct = default)
    {
        // 1. Extraer intención y nombre del producto con el LLM
        var intent = await predictionRouter.ParseAsync(question, ct);

        if (!intent.IsPredictionRequest || string.IsNullOrEmpty(intent.EntityName))
        {
            return new AskResult(false, "", "No detecté de qué producto quieres el pronóstico. Por favor especifica el nombre (ej. Chang, Chai).", false);
        }

        // 2. Ejecutar la matemática con ML.NET (FastTree)
        intent = await forecaster.PredictAsync(intent);

        // 3. Redactar el análisis en lenguaje natural
        var humanizedText = await humanizer.HumanizeAsync(intent, ct);

        // 4. Empaquetar el JSON exactamente como la UI lo espera
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
    // HELPERS (Intactos)
    // ==========================================
    private string BuildPrompt(string question, RetrievalContext context)
    {
        var prompt = "<|im_start|>system\nEres un desarrollador experto en T-SQL para SQL Server. Tu única tarea es devolver código T-SQL válido. NO des explicaciones, NO platiques, SOLO devuelve el código.\n" +
                     "REGLA CRÍTICA DE LA PLANTA: La tabla de detalles de órdenes se llama exactamente OrderDetails (SIN ESPACIOS). NUNCA generes [Order Details].\n\n";

        if (context.SchemaDocs.Any())
        {
            prompt += "=== ESQUEMA DE LA BASE DE DATOS ===\n";
            foreach (var doc in context.SchemaDocs) prompt += $"{doc.Doc.DocText}\n";
        }

        if (context.Examples.Any())
        {
            prompt += "\n=== EJEMPLOS DE CONSULTAS CORRECTAS ===\n";
            foreach (var ex in context.Examples) prompt += $"Pregunta: {ex.Example.Question}\nSQL:\n{ex.Example.Sql}\n\n";
        }

        prompt += $"<|im_end|>\n<|im_start|>user\nGenera el T-SQL para la siguiente solicitud: {question}<|im_end|>\n<|im_start|>assistant\n```sql\n";
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