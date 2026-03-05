using Microsoft.AspNetCore.Mvc;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using VannaLight.Api.Services; // <- para WiDocIngestor

namespace VannaLight.Api.Controllers;

public record TrainRequest(string Question, string SqlText);

[ApiController]
[Route("api/[controller]")]
public class AdminController(IConfiguration config, WiDocIngestor wiIngestor) : ControllerBase
{
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

    // 2. Guardar la corrección en la memoria RAG (SQLite)
    [HttpPost("train")]
    public async Task<IActionResult> Train([FromBody] TrainRequest request)
    {
        if (request == null)
            return BadRequest(new { Error = "Body inválido." });

        string cleanQuestion = request.Question?
            .Replace("\0", "")
            .Replace("\uFFFF", "")
            .Replace("홚", "")
            .Trim() ?? "";

        string cleanSql = request.SqlText?
            .Replace("\0", "")
            .Replace("\uFFFF", "")
            .Trim() ?? "";

        if (string.IsNullOrEmpty(cleanQuestion) || string.IsNullOrEmpty(cleanSql))
            return BadRequest(new { Error = "La pregunta o el SQL están vacíos o corruptos." });

        string sqlitePath = config["Paths:Sqlite"] ?? "vanna_memory.db";
        await using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        await connection.OpenAsync();

        const string sql = @"INSERT INTO TrainingExamples (Question, Sql, CreatedUtc, LastUsedUtc) 
                             VALUES (@Question, @Sql, @CreatedUtc, @LastUsedUtc)";

        await connection.ExecuteAsync(sql, new
        {
            Question = cleanQuestion,
            Sql = cleanSql,
            CreatedUtc = DateTime.UtcNow,
            LastUsedUtc = DateTime.UtcNow
        });

        return Ok(new { Message = "Entrenamiento guardado en la memoria RAG exitosamente." });
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