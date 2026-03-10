using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using VannaLight.Api.Services;
using VannaLight.Api.Hubs;
using VannaLight.Api.Contracts;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;
using VannaLight.Core.Settings;

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
    IConfiguration config,
    RuntimeDbOptions runtimeOptions, // Inyectamos la config de SQLite para la caché
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
            // AHORA BUSCAMOS LA CACHÉ EN SQLITE (vanna_runtime.db) EN LUGAR DEL ERP
            using var sqliteConn = new SqliteConnection($"Data Source={runtimeOptions.DbPath};");

            var cachedJob = await sqliteConn.QueryFirstOrDefaultAsync<CachedQuestionJobRow>(
                            @"SELECT JobId, SqlText, ResultJson
                              FROM QuestionJobs
                              WHERE UserId = @userId
                                AND Question = @q
                                AND Status = 'Completed'
                              ORDER BY UpdatedUtc DESC
                              LIMIT 1",
                            new
                            {
                                userId,
                                q = cleanQuestion
                            }
            );

            if (cachedJob != null && !string.IsNullOrEmpty(cachedJob.SqlText))
            {
                // Si hubo hit en caché, ejecutamos el SQL contra el ERP real (SQL Server)
                IEnumerable<dynamic> queryResults = Array.Empty<dynamic>();
                try
                {
                    string sqlConn = config.GetConnectionString("OperationalDb") ?? throw new Exception("Falta BD");
                    using var sqlServerConn = new SqlConnection(sqlConn);
                    queryResults = await sqlServerConn.QueryAsync(cachedJob.SqlText);
                }
                catch
                {
                    // Si falla la ejecución contra el ERP, dejamos que el flujo normal lo procese
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

                    return Accepted(new { Message = "Respuesta servida desde la caché local." });
                }
            }
        }

        // --- 2) FLUJO NORMAL: ENCOLAR TRABAJO ---

        // CORRECCIÓN: Usamos la nueva firma de la interfaz que devuelve el JobId generado
        var jobId = await jobStore.CreateJobAsync(userId, "User", cleanQuestion);

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