using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using VannaLight.Api.Services;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;
using VannaLight.Core.Settings;
using VannaLight.Core.UseCases;

namespace VannaLight.Api.Controllers;

public record TrainRequest(
    string JobId,
    string Question,
    string SqlText,
    string? FeedbackComment);

public record LlmProfileUpdateRequest(
    int? GpuLayerCount,
    uint? ContextSize,
    int? BatchSize,
    int? UBatchSize,
    int? Threads
);

public record AllowedObjectUpsertRequest(
    string Domain,
    string SchemaName,
    string ObjectName,
    string ObjectType,
    bool IsActive,
    string? Notes);

public record AllowedObjectStatusRequest(
    bool IsActive);

public record BusinessRuleUpsertRequest(
    long Id,
    string Domain,
    string RuleKey,
    string RuleText,
    int Priority,
    bool IsActive);

public record BusinessRuleStatusRequest(
    bool IsActive);

[ApiController]
[Route("api/[controller]")]
public class AdminController(
    IJobStore jobStore,
    IAllowedObjectStore allowedObjectStore,
    ILlmProfileStore profileStore,
    WiDocIngestor wiIngestor,
    IBusinessRuleStore businessRuleStore,
    SqliteOptions sqliteOptions,
    OperationalDbOptions operationalDbOptions,
    IngestUseCase ingestUseCase,
    TrainExampleUseCase useCase,
    ILogger<AdminController> logger) : ControllerBase
{
    // ==========================================
    // 1. RAG Y ENTRENAMIENTO
    // ==========================================

    [HttpPost("train")]
    public async Task<IActionResult> Train([FromBody] TrainRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { Error = "Body inválido." });

        if (!Guid.TryParse(request.JobId, out var jobId))
            return BadRequest(new { Error = "JobId inválido." });

        try
        {
            await useCase.TrainAsync(request.Question, request.SqlText, ct);

            var updated = await jobStore.UpdateJobReviewAsync(
                jobId,
                request.SqlText,
                verificationStatus: "Verified",
                comment: request.FeedbackComment,
                ct);

            if (!updated)
                return NotFound(new { Error = "No se encontró el job a actualizar en runtime." });

            return Ok(new
            {
                Message = "Entrenamiento guardado en la memoria RAG y runtime actualizado correctamente.",
                JobId = request.JobId,
                VerificationStatus = "Verified",
                TrainingExampleSaved = true
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    [HttpPost("reindex-wi")]
    public async Task<IActionResult> ReindexWi(CancellationToken ct)
    {
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

    [HttpPost("reindex-schema")]
    public async Task<IActionResult> ReindexSchema(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(operationalDbOptions.ConnectionString))
        {
            return BadRequest(new
            {
                Error = "OperationalDbOptions.ConnectionString no está configurado."
            });
        }

        if (string.IsNullOrWhiteSpace(sqliteOptions.DbPath))
        {
            return BadRequest(new
            {
                Error = "SqliteOptions.DbPath no está configurado."
            });
        }

        try
        {
            logger.LogInformation(
                "Iniciando reindexación de schema. SqliteDbPath: {SqliteDbPath}",
                sqliteOptions.DbPath);

            await ingestUseCase.ExecuteAsync(
                operationalDbOptions.ConnectionString,
                sqliteOptions.DbPath,
                ct);

            logger.LogInformation("Reindexación de schema completada correctamente.");

            return Ok(new
            {
                Message = "Reindexación de schema completada correctamente."
            });
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("La reindexación de schema fue cancelada.");

            return StatusCode(StatusCodes.Status499ClientClosedRequest, new
            {
                Error = "La reindexación fue cancelada."
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error durante la reindexación de schema.");

            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                Error = "Ocurrió un error al reindexar el schema.",
                Detail = ex.Message
            });
        }
    }

    // ==========================================
    // 2. HISTORIAL DE TRABAJOS (SLIM)
    // ==========================================

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(CancellationToken ct)
    {
        // FIX: Forzamos el modo "Data" (SQL).
        // El Admin de RAG NUNCA debe ver las predicciones de ML.NET.
        var jobs = await jobStore.GetRecentJobsAsync(20, "Data", ct);
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
        if (!success)
            return NotFound(new { Error = "Perfil no encontrado." });

        return Ok(new
        {
            Message = "Perfil activado correctamente.",
            Warning = "Requiere reiniciar la API para aplicar los cambios de VRAM de llama.cpp."
        });
    }

    [HttpPut("llm-profiles/{id}")]
    public async Task<IActionResult> UpdateLlmProfile(int id, [FromBody] LlmProfileUpdateRequest req, CancellationToken ct)
    {
        if (req is null)
            return BadRequest(new { Error = "Body inválido." });

        var success = await profileStore.UpdateAsync(
            id,
            req.GpuLayerCount,
            req.ContextSize,
            req.BatchSize,
            req.UBatchSize,
            req.Threads,
            ct);

        if (!success)
            return NotFound(new { Error = "Perfil no encontrado." });

        return Ok(new { Message = "Ajustes del perfil guardados exitosamente." });
    }

    // ==========================================
    // 4. ALLOWED OBJECTS
    // ==========================================

    [HttpGet("allowed-objects")]
    public async Task<IActionResult> GetAllowedObjects([FromQuery] string domain, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return BadRequest(new { Error = "El parámetro domain es requerido." });

        var items = await allowedObjectStore.GetAllObjectsAsync(domain, ct);
        return Ok(items);
    }

    [HttpPost("allowed-objects")]
    public async Task<IActionResult> UpsertAllowedObject([FromBody] AllowedObjectUpsertRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { Error = "Body inválido." });

        if (string.IsNullOrWhiteSpace(request.Domain))
            return BadRequest(new { Error = "Domain es requerido." });

        if (string.IsNullOrWhiteSpace(request.SchemaName))
            return BadRequest(new { Error = "SchemaName es requerido." });

        if (string.IsNullOrWhiteSpace(request.ObjectName))
            return BadRequest(new { Error = "ObjectName es requerido." });

        var id = await allowedObjectStore.UpsertAsync(
            new AllowedObject
            {
                Domain = request.Domain,
                SchemaName = request.SchemaName,
                ObjectName = request.ObjectName,
                ObjectType = request.ObjectType,
                IsActive = request.IsActive,
                Notes = request.Notes
            },
            ct);

        return Ok(new
        {
            Message = "AllowedObject guardado correctamente.",
            Id = id
        });
    }

    [HttpPatch("allowed-objects/{id:long}/status")]
    public async Task<IActionResult> SetAllowedObjectStatus(long id, [FromBody] AllowedObjectStatusRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { Error = "Body inválido." });

        var updated = await allowedObjectStore.SetIsActiveAsync(id, request.IsActive, ct);

        if (!updated)
            return NotFound(new { Error = "AllowedObject no encontrado." });

        return Ok(new
        {
            Message = "Estatus actualizado correctamente.",
            Id = id,
            IsActive = request.IsActive
        });
    }

    // =============================================
    // 5. BUSINESS RULES
    // =============================================

    [HttpGet("business-rules")]
    public async Task<IActionResult> GetBusinessRules([FromQuery] string domain, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return BadRequest(new { Error = "El parámetro domain es requerido." });

        var rules = await businessRuleStore.GetAllRulesAsync(
            sqliteOptions.DbPath,
            domain,
            ct);

        return Ok(rules);
    }

    [HttpPost("business-rules")]
    public async Task<IActionResult> UpsertBusinessRule([FromBody] BusinessRuleUpsertRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { Error = "Body inválido." });

        if (string.IsNullOrWhiteSpace(request.Domain))
            return BadRequest(new { Error = "Domain es requerido." });

        if (string.IsNullOrWhiteSpace(request.RuleKey))
            return BadRequest(new { Error = "RuleKey es requerido." });

        if (string.IsNullOrWhiteSpace(request.RuleText))
            return BadRequest(new { Error = "RuleText es requerido." });

        if (request.Priority < 0)
            return BadRequest(new { Error = "Priority debe ser mayor o igual a 0." });

        var id = await businessRuleStore.UpsertAsync(
            sqliteOptions.DbPath,
            new BusinessRule
            {
                Id = request.Id,
                Domain = request.Domain,
                RuleKey = request.RuleKey,
                RuleText = request.RuleText,
                Priority = request.Priority,
                IsActive = request.IsActive
            },
            ct);

        return Ok(new
        {
            Message = "BusinessRule guardada correctamente.",
            Id = id
        });
    }

    [HttpPatch("business-rules/{id:long}/status")]
    public async Task<IActionResult> SetBusinessRuleStatus(long id, [FromBody] BusinessRuleStatusRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { Error = "Body inválido." });

        var updated = await businessRuleStore.SetIsActiveAsync(
            sqliteOptions.DbPath,
            id,
            request.IsActive,
            ct);

        if (!updated)
            return NotFound(new { Error = "BusinessRule no encontrada." });

        return Ok(new
        {
            Message = "Estatus actualizado correctamente.",
            Id = id,
            IsActive = request.IsActive
        });
    }
}