using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Dapper;
using Microsoft.Data.SqlClient;
using VannaLight.Api.Data;
using VannaLight.Api.Services;
using VannaLight.Api.Hubs;

namespace VannaLight.Api.Controllers;

public record AskRequest(string Question, string? UserId, string? ConnectionId);

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
        {
            return BadRequest(new { Error = "La pregunta no puede estar vacía." });
        }

        string sqlConn = config.GetConnectionString("OperationalDb") ?? throw new Exception("Falta BD");
        using var connection = new SqlConnection(sqlConn);
        string cleanQuestion = request.Question.Trim();

        // --- 1. SISTEMA DE CACHÉ (Cero duplicados, cero uso de GPU) ---
        // Buscamos si esta pregunta exacta ya se respondió con éxito anteriormente
        // --- 1. SISTEMA DE CACHÉ (Cero duplicados, cero uso de GPU) ---
        // MAGIA AQUÍ: Agregamos <QuestionJob> para que deje de ser 'dynamic'
        var cachedJob = await connection.QueryFirstOrDefaultAsync<QuestionJob>(
            @"SELECT TOP 1 JobId, SqlText 
              FROM QuestionJobs 
              WHERE Question = @q AND Status = 'Completed' 
              ORDER BY CreatedUtc DESC",
            new { q = cleanQuestion }
        );

        if (cachedJob != null && !string.IsNullOrEmpty(cachedJob.SqlText))
        {
            // ¡CACHÉ HIT! 
            IEnumerable<dynamic> queryResults = Array.Empty<dynamic>();
            try
            {
                // Ahora cachedJob.SqlText es un string 100% real, no fake
                queryResults = await connection.QueryAsync(cachedJob.SqlText);
            }
            catch { /* Si falla la ejecución, lo ignoramos y dejamos que fluya normal */ }

            // 2. Respondemos INMEDIATAMENTE por SignalR
            if (!string.IsNullOrEmpty(request.ConnectionId))
            {
                await hubContext.Clients.Client(request.ConnectionId).SendAsync("JobCompleted", new
                {
                    JobId = cachedJob.JobId,
                    Sql = cachedJob.SqlText,
                    Data = queryResults
                });
            }

            // 3. Salimos aquí. NO creamos registro nuevo y NO mandamos al Worker.
            return Accepted(new { Message = "Respuesta servida desde la caché en milisegundos." });
        }


        // --- 2. FLUJO NORMAL (Si no está en caché, la LLM debe trabajar) ---
        var jobId = Guid.NewGuid();
        var userId = request.UserId ?? "UsuarioPlanta_01";

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

        // Guardar en la bitácora
        await jobStore.CreateJobAsync(job);

        // Mandar a la cola de la GPU
        var workItem = new AskWorkItem(jobId, cleanQuestion, userId, request.ConnectionId ?? string.Empty);
        await queue.EnqueueAsync(workItem);

        return Accepted(new { JobId = jobId, Status = "Queued" });
    }

    [HttpGet("status/{jobId:guid}")]
    public async Task<IActionResult> GetStatusAsync(Guid jobId)
    {
        var job = await jobStore.GetJobAsync(jobId);
        if (job == null) return NotFound(new { Error = "Trabajo no encontrado." });
        return Ok(new { job.JobId, job.Status, job.SqlText, job.ErrorText });
    }
}