using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Dapper;
using Microsoft.Data.SqlClient;
using VannaLight.Api.Data;
using VannaLight.Api.Services;
using VannaLight.Api.Hubs;
using VannaLight.Api.Contracts;

namespace VannaLight.Api.Controllers;

//public enum AskMode { Data, Docs, Predict }

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
    IConfiguration config,
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
            string sqlConn = config.GetConnectionString("OperationalDb") ?? throw new Exception("Falta BD");
            using var connection = new SqlConnection(sqlConn);

            var cachedJob = await connection.QueryFirstOrDefaultAsync<QuestionJob>(
                @"SELECT TOP 1 JobId, SqlText 
                  FROM QuestionJobs 
                  WHERE Question = @q AND Status = 'Completed' 
                  ORDER BY CreatedUtc DESC",
                new { q = cleanQuestion }
            );

            if (cachedJob != null && !string.IsNullOrEmpty(cachedJob.SqlText))
            {
                // Cache hit: intentamos ejecutar el SQL cacheado para devolver data
                IEnumerable<dynamic> queryResults = Array.Empty<dynamic>();
                try
                {
                    queryResults = await connection.QueryAsync(cachedJob.SqlText);
                }
                catch
                {
                    // Si falla, dejamos fluir al worker (podría ser un cambio de esquema/datos)
                    // OJO: No salimos aquí si hubo excepción.
                    cachedJob = null;
                }

                if (cachedJob != null)
                {
                    if (!string.IsNullOrEmpty(connectionId))
                    {
                        await hubContext.Clients.Client(connectionId).SendAsync("JobCompleted", new
                        {
                            JobId = cachedJob.JobId,
                            Mode = AskMode.Data.ToString(),
                            Sql = cachedJob.SqlText,
                            Data = queryResults
                        });
                    }

                    return Accepted(new { Message = "Respuesta servida desde la caché en milisegundos." });
                }
            }
        }

        // --- 2) FLUJO NORMAL: ENCOLAR TRABAJO ---
        var jobId = Guid.NewGuid();

        var job = new QuestionJob
        {
            JobId = jobId,
            UserId = userId,
            Role = "User",
            Question = cleanQuestion,
            Status = "Queued",
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        await jobStore.CreateJobAsync(job);

        // IMPORTANTe: AskWorkItem ahora debe incluir Mode
        var workItem = new AskWorkItem(jobId, cleanQuestion, userId, connectionId, request.Mode);
        await queue.EnqueueAsync(workItem);

        return Accepted(new { JobId = jobId, Status = "Queued", Mode = request.Mode.ToString() });
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