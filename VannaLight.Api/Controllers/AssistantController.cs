using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;
using VannaLight.Api.Contracts;
using VannaLight.Api.Hubs;
using VannaLight.Api.Services;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Api.Controllers;

public record AskRequest(
    string Question,
    string? UserId,
    string? ConnectionId,
    AskMode Mode = AskMode.Data
);

[ApiController]
[Route("api/[controller]")]
public class AssistantController(
    IJobStore jobStore,
    IAskRequestQueue queue,
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

        // --- 1) CACHÉ: SOLO PARA DATA (SQL) ---
        if (request.Mode == AskMode.Data)
        {
            // Toda la complejidad de Dapper, SQLite y SQL Server se delega al servicio
            var (sql, data) = await cacheService.TryGetCachedResultAsync(cleanQuestion, userId, default);

            if (sql != null)
            {
                if (!string.IsNullOrEmpty(connectionId))
                {
                    await hubContext.Clients.Client(connectionId).SendAsync("JobCompleted", new
                    {
                        JobId = Guid.NewGuid(), // Generamos un ID virtual para la UI
                        Mode = AskMode.Data.ToString(),
                        Sql = sql,
                        Data = data
                    });
                }

                return Accepted(new { Message = "Respuesta servida desde la caché local." });
            }
        }

        // --- 2) FLUJO NORMAL: ENCOLAR TRABAJO ---
        // FIX: Guardamos explícitamente el "Mode" en la base de datos para no mezclar
        var jobId = await jobStore.CreateJobAsync(userId, "User", cleanQuestion, request.Mode.ToString());

        var workItem = new AskWorkItem(jobId, cleanQuestion, userId, connectionId, request.Mode);
        await queue.EnqueueAsync(workItem);

        return Accepted(new { JobId = jobId, Status = "Queued", Mode = request.Mode.ToString() });
    }

    // --- NUEVO: Endpoint para cargar el historial en el Chat separando SQL y ML ---
    [HttpGet("history")]
    public async Task<IActionResult> GetHistoryAsync([FromQuery] string mode = "Data", CancellationToken ct = default)
    {
        // El frontend de chat pasará '?mode=Data' o '?mode=Predict' según el tab activo
        var jobs = await jobStore.GetRecentJobsAsync(50, mode, ct);
        return Ok(jobs);
    }

    [HttpGet("status/{jobId:guid}")]
    public async Task<IActionResult> GetStatusAsync(Guid jobId)
    {
        var job = await jobStore.GetJobAsync(jobId);
        if (job == null) return NotFound(new { Error = "Trabajo no encontrado." });

        return Ok(new
        {
            job.JobId,
            job.Status,
            job.SqlText,
            job.ErrorText
        });
    }
}