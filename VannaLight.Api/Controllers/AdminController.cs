using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;
using VannaLight.Api.Services;
using VannaLight.Core.Abstractions;
using VannaLight.Core.UseCases;

namespace VannaLight.Api.Controllers;

public record TrainRequest(string Question, string SqlText);

public record LlmProfileUpdateRequest(
    int? GpuLayerCount,
    uint? ContextSize,
    int? BatchSize,
    int? UBatchSize,
    int? Threads
);

[ApiController]
[Route("api/[controller]")]
public class AdminController(
    IJobStore jobStore,            // <-- Capa de datos para historial inyectada
    ILlmProfileStore profileStore, // <-- Capa de datos para perfiles de IA inyectada
    WiDocIngestor wiIngestor,
    TrainExampleUseCase useCase) : ControllerBase
{
    // ==========================================
    // 1. RAG Y ENTRENAMIENTO
    // ==========================================

    [HttpPost("train")]
    public async Task<IActionResult> Train([FromBody] TrainRequest request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { Error = "Body inválido." });

        try
        {
            await useCase.TrainAsync(request.Question, request.SqlText, ct);
            return Ok(new { Message = "Entrenamiento guardado en la memoria RAG exitosamente." });
        }
        catch (ArgumentException ex) { return BadRequest(new { Error = ex.Message }); }
    }

    [HttpPost("reindex-wi")]
    public async Task<IActionResult> ReindexWi(CancellationToken ct)
    {
        var result = await wiIngestor.ReindexAsync(ct);
        return Ok(new { Message = "Reindex de WI completado.", result.TotalFiles, result.Indexed, result.Skipped, result.Errors });
    }

    // ==========================================
    // 2. HISTORIAL DE TRABAJOS (SLIM)
    // ==========================================

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(CancellationToken ct)
    {
        // Se delega la búsqueda a SQLite a través de la abstracción (IJobStore)
        var jobs = await jobStore.GetRecentJobsAsync(20, ct);
        return Ok(jobs);
    }

    // ==========================================
    // 3. GESTIÓN DE PERFILES LLM (SLIM)
    // ==========================================

    [HttpGet("llm-profiles")]
    public async Task<IActionResult> GetLlmProfiles(CancellationToken ct)
    {
        var profiles = await profileStore.GetAllAsync(ct);
        return Ok(profiles);
    }

    [HttpPost("llm-profiles/{id}/activate")]
    public async Task<IActionResult> ActivateLlmProfile(int id, CancellationToken ct)
    {
        var success = await profileStore.ActivateAsync(id, ct);
        if (!success) return NotFound(new { Error = "Perfil no encontrado." });

        return Ok(new
        {
            Message = "Perfil activado correctamente.",
            Warning = "Requiere reiniciar la API para aplicar los cambios de VRAM de llama.cpp."
        });
    }

    [HttpPut("llm-profiles/{id}")]
    public async Task<IActionResult> UpdateLlmProfile(int id, [FromBody] LlmProfileUpdateRequest req, CancellationToken ct)
    {
        var success = await profileStore.UpdateAsync(
            id,
            req.GpuLayerCount,
            req.ContextSize,
            req.BatchSize,
            req.UBatchSize,
            req.Threads,
            ct);

        if (!success) return NotFound(new { Error = "Perfil no encontrado." });
        return Ok(new { Message = "Ajustes del perfil guardados exitosamente." });
    }
}