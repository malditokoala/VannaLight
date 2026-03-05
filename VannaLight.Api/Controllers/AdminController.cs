using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using VannaLight.Api.Services; // <- para WiDocIngestor
using VannaLight.Core.UseCases;

namespace VannaLight.Api.Controllers;

public record TrainRequest(string Question, string SqlText);

[ApiController]
[Route("api/[controller]")]
public class AdminController(IConfiguration config, WiDocIngestor wiIngestor, TrainExampleUseCase useCase) : ControllerBase
{
    private readonly TrainExampleUseCase _useCase = useCase;



    [HttpPost("train")]
    public async Task<IActionResult> Train([FromBody] TrainRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { Error = "Body inválido." });

        try
        {
            await _useCase.TrainAsync(request.Question, request.SqlText, ct);
            return Ok(new { Message = "Entrenamiento guardado en la memoria RAG exitosamente." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    // 1. Obtener el historial de la tabla operativa (SQL Server)
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory()
    {
        string sqlConn = config.GetConnectionString("OperationalDb") ?? throw new Exception("Falta SQL Conn");
        using var connection = new SqlConnection(sqlConn);

        var jobs = await connection.QueryAsync(@"
            SELECT TOP 20 JobId, Question, SqlText, Status, ErrorText, CreatedUtc 
            FROM QuestionJobs 
            ORDER BY CreatedUtc DESC");

        return Ok(jobs);
    }

  

// 3. (FASE 3) Reindexar Work Instructions (PDFs) a SQLite
[HttpPost("reindex-wi")]
    public async Task<IActionResult> ReindexWi(CancellationToken ct)
    {
        // WiRootPath lo tomará del appsettings: Docs:WiRootPath
        var result = await wiIngestor.ReindexAsync(ct);

        return Ok(new
        {
            Message = "Reindex de WI completado.",
            result.TotalFiles,
            result.Indexed,
            result.Skipped,
            result.Errors
        });
    }
}