using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;
using VannaLight.Core.Settings;

namespace VannaLight.Core.UseCases;

// 1. EL RECORD SE LLAMA AskResult (Esta es la "caja" de la respuesta)
public record AskResult(bool Success, string Sql, string? Error, bool PassedDryRun, long? ReviewId = null);

// 2. LA CLASE SE LLAMA AskUseCase (Este es el motor)
public class AskUseCase(
    IRetriever retriever,
    ILlmClient llmClient,
    ISqlValidator validator,
    ISqlDryRunner dryRunner,
    IReviewStore reviewStore,
    AppSettings settings)
{
    public async Task<AskResult> ExecuteAsync(
        string question,
        string sqlitePath,
        string sqlServerConnString,
        CancellationToken ct = default)
    {
        var context = await retriever.RetrieveAsync(sqlitePath, question, ct);
        var prompt = BuildPrompt(question, context);

        var rawSql = await llmClient.GenerateSqlAsync(prompt, ct);
        var cleanSql = CleanLlmOutput(rawSql);

        // Validación estática
        if (!validator.TryValidate(cleanSql, out var validationError))
        {
            var reviewId = await reviewStore.EnqueueAsync(sqlitePath, question, cleanSql, validationError, ReviewReason.Unsafe.ToString(), ct);
            return new AskResult(false, cleanSql, $"Validación fallida: {validationError} (Guardado en ReviewQueue #{reviewId})", false, reviewId);
        }

        // Validación dinámica (Dry-Run)
        bool passedDryRun = false;
        if (settings.Security.DryRunEnabledByDefault)
        {
            var (ok, error) = await dryRunner.DryRunAsync(sqlServerConnString, cleanSql, ct);
            if (!ok)
            {
                var reviewId = await reviewStore.EnqueueAsync(sqlitePath, question, cleanSql, error, ReviewReason.NotCompiling.ToString(), ct);
                return new AskResult(false, cleanSql, $"Error de compilación en SQL Server: {error} (Guardado en ReviewQueue #{reviewId})", false, reviewId);
            }
            passedDryRun = true;
        }

        return new AskResult(true, cleanSql, null, passedDryRun);
    }

    private string BuildPrompt(string question, RetrievalContext context)
    {
        // Usamos el formato ChatML estricto que Qwen2.5 y Llama3 entienden
        var prompt = "<|im_start|>system\nEres un desarrollador experto en T-SQL para SQL Server. Tu única tarea es devolver código T-SQL válido. NO des explicaciones, NO platiques, SOLO devuelve el código.\n\n";

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

        // Cerramos el sistema, abrimos el usuario, y forzamos al asistente a empezar con código
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