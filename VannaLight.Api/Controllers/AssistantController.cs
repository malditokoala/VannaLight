using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VannaLight.Api.Contracts;
using VannaLight.Api.Hubs;
using VannaLight.Api.Services;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Api.Controllers;

public sealed record AskRequest
{
    public string Question { get; init; } = string.Empty;
    public string? UserId { get; init; }
    public string? ConnectionId { get; init; }
    public string? TenantKey { get; init; }
    public string? Domain { get; init; }
    public string? ConnectionName { get; init; }
    public AskMode Mode { get; init; } = AskMode.Data;
}

public record FeedbackRequest(
    Guid JobId,
    string Feedback
);

[ApiController]
[Route("api/[controller]")]
public class AssistantController(
    IJobStore jobStore,
    IAskRequestQueue queue,
    IExecutionContextResolver executionContextResolver,
    ISqlCacheService cacheService, // <-- NUEVO: Inyectamos el servicio de caché (SOLID)
    IHubContext<AssistantHub> hubContext) : ControllerBase
{
    [HttpPost("ask")]
    public async Task<IActionResult> AskAsync([FromBody] AskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest(new { Error = "La pregunta no puede estar vacía." });

        string cleanQuestion = request.Question.Trim();
        string userId = request.UserId ?? "UsuarioPlanta_01";
        string connectionId = request.ConnectionId ?? string.Empty;
        var executionContext = await executionContextResolver.ResolveAsync(
            request.TenantKey,
            request.Domain,
            request.ConnectionName,
            HttpContext.RequestAborted);

        // --- 1) CACHÉ: SOLO PARA DATA (SQL) ---
        if (request.Mode == AskMode.Data)
        {
            // Toda la complejidad de Dapper, SQLite y SQL Server se delega al servicio
            var (sql, data) = await cacheService.TryGetCachedResultAsync(cleanQuestion, userId, default);

            if (sql != null)
            {
                var cachedJobId = await jobStore.CreateJobAsync(
                    userId,
                    "User",
                    cleanQuestion,
                    request.Mode.ToString(),
                    executionContext,
                    HttpContext.RequestAborted);
                var cachedResultJson = JsonSerializer.Serialize(data);

                await jobStore.UpdateJobAsync(
                    cachedJobId,
                    "Completed",
                    sql,
                    cachedResultJson,
                    null,
                    HttpContext.RequestAborted);

                if (!string.IsNullOrEmpty(connectionId))
                {
                    await hubContext.Clients.Client(connectionId).SendAsync("JobCompleted", new
                    {
                        JobId = cachedJobId,
                        Mode = AskMode.Data.ToString(),
                        Sql = sql,
                        Data = data
                    });
                }

                return Accepted(new
                {
                    JobId = cachedJobId,
                    Status = "Completed",
                    Mode = request.Mode.ToString(),
                    Message = "Respuesta servida desde la caché local."
                });
            }
        }

        // --- 2) FLUJO NORMAL: ENCOLAR TRABAJO ---
        // FIX: Guardamos explícitamente el "Mode" en la base de datos para no mezclar
        var jobId = await jobStore.CreateJobAsync(
            userId,
            "User",
            cleanQuestion,
            request.Mode.ToString(),
            executionContext,
            HttpContext.RequestAborted);

        var workItem = new AskWorkItem(
            jobId,
            cleanQuestion,
            userId,
            connectionId,
            request.Mode,
            executionContext.TenantKey,
            executionContext.Domain,
            executionContext.ConnectionName);
        await queue.EnqueueAsync(workItem);

        return Accepted(new
        {
            JobId = jobId,
            Status = "Queued",
            Mode = request.Mode.ToString(),
            executionContext.TenantKey,
            executionContext.Domain,
            executionContext.ConnectionName
        });
    }

    // --- NUEVO: Endpoint para cargar el historial en el Chat separando SQL y ML ---
    [HttpGet("history")]
    public async Task<IActionResult> GetHistoryAsync([FromQuery] string mode = "Data", CancellationToken ct = default)
    {
        // El frontend de chat pasará '?mode=Data' o '?mode=Predict' según el tab activo
        var jobs = await jobStore.GetRecentJobsAsync(50, mode, ct);
        return Ok(jobs);
    }

    [HttpPost("feedback")]
    public async Task<IActionResult> SubmitFeedbackAsync([FromBody] FeedbackRequest request, CancellationToken ct)
    {
        if (request.JobId == Guid.Empty)
            return BadRequest(new { Error = "JobId inválido." });

        var feedback = NormalizeFeedback(request.Feedback);
        if (feedback is null)
            return BadRequest(new { Error = "Feedback inválido. Usa 'Up' o 'Down'." });

        var updated = await jobStore.SetUserFeedbackAsync(request.JobId, feedback, ct);
        if (!updated)
            return NotFound(new { Error = "Trabajo no encontrado." });

        return Ok(new
        {
            JobId = request.JobId,
            UserFeedback = feedback,
            FeedbackUtc = DateTime.UtcNow
        });
    }

    [HttpGet("status/{jobId:guid}")]
    public async Task<IActionResult> GetStatusAsync(Guid jobId)
    {
        var job = await jobStore.GetJobAsync(jobId);
        if (job == null) return NotFound(new { Error = "Trabajo no encontrado." });

        return Ok(new
        {
            job.JobId,
            job.TenantKey,
            job.Domain,
            job.ConnectionName,
            job.Question,
            job.Status,
            job.Mode,
            job.SqlText,
            job.ResultJson,
            job.ErrorText,
            job.VerificationStatus,
            job.UserFeedback
        });
    }

    private static string? NormalizeFeedback(string? feedback)
    {
        if (string.IsNullOrWhiteSpace(feedback))
            return null;

        return feedback.Trim().ToLowerInvariant() switch
        {
            "up" => "Up",
            "down" => "Down",
            _ => null
        };
    }
}
