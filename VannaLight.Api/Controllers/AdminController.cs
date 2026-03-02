using Microsoft.AspNetCore.Mvc;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;

namespace VannaLight.Api.Controllers;

public record TrainRequest(string Question, string SqlText);

[ApiController]
[Route("api/[controller]")]
public class AdminController(IConfiguration config) : ControllerBase
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
        // 1. FILTRO SANITARIO: Limpiamos la basura de memoria (Null bytes y Unicode inválido)
        string cleanQuestion = request.Question?
            .Replace("\0", "")        // Quita los bytes nulos
            .Replace("\uFFFF", "")    // Quita los marcadores inválidos
            .Replace("홚", "")        // Quita el artefacto asiático de memoria que se coló
            .Trim() ?? "";

        string cleanSql = request.SqlText?
            .Replace("\0", "")
            .Replace("\uFFFF", "")
            .Trim() ?? "";

        if (string.IsNullOrEmpty(cleanQuestion) || string.IsNullOrEmpty(cleanSql))
        {
            return BadRequest(new { Error = "La pregunta o el SQL están vacíos o corruptos." });
        }

        string sqlitePath = config["Paths:Sqlite"] ?? "vanna_memory.db";
        using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        await connection.OpenAsync();

        // 2. Usamos las variables LIMPIAS en el INSERT
        // 1. Agregamos LastUsedUtc a la lista de columnas y valores
        string sql = @"INSERT INTO TrainingExamples (Question, Sql, CreatedUtc, LastUsedUtc) 
                       VALUES (@Question, @Sql, @CreatedUtc, @LastUsedUtc)";

        // 2. Le pasamos la hora actual a ambos campos de fecha
        await connection.ExecuteAsync(sql, new
        {
            Question = cleanQuestion,
            Sql = cleanSql,
            CreatedUtc = DateTime.UtcNow,
            LastUsedUtc = DateTime.UtcNow // <-- ¡El pase VIP final para SQLite!
        });

        return Ok(new { Message = "Entrenamiento guardado en la memoria RAG exitosamente." });
    }
}
