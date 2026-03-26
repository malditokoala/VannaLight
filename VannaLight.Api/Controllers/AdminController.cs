using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
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

public record SemanticHintUpsertRequest(
    long Id,
    string Domain,
    string HintKey,
    string HintType,
    string? DisplayName,
    string? ObjectName,
    string? ColumnName,
    string HintText,
    int Priority,
    bool IsActive);

public record SemanticHintStatusRequest(
    bool IsActive);

public record QueryPatternUpsertRequest(
    long Id,
    string Domain,
    string PatternKey,
    string IntentName,
    string? Description,
    string SqlTemplate,
    int? DefaultTopN,
    string? MetricKey,
    string? DimensionKey,
    string? DefaultTimeScopeKey,
    int Priority,
    bool IsActive);

public record QueryPatternStatusRequest(
    bool IsActive);

public record QueryPatternTermUpsertRequest(
    long Id,
    long PatternId,
    string Term,
    string TermGroup,
    string MatchMode,
    bool IsRequired,
    bool IsActive);

public record QueryPatternTermStatusRequest(
    bool IsActive);

public record SystemConfigEntryUpsertRequest(
    int Id,
    string Section,
    string Key,
    string? Value,
    string ValueType,
    bool IsEditableInUi,
    string? ValidationRule,
    string? Description);

public record SystemConfigBulkUpsertRequest(
    IReadOnlyList<SystemConfigEntryUpsertRequest> Entries);

[ApiController]
[Route("api/[controller]")]
public class AdminController(
    IJobStore jobStore,
    ISystemConfigProvider systemConfigProvider,
    ISystemConfigStore systemConfigStore,
    IAllowedObjectStore allowedObjectStore,
    ILlmProfileStore profileStore,
    WiDocIngestor wiIngestor,
    IBusinessRuleStore businessRuleStore,
    ISemanticHintStore semanticHintStore,
    IQueryPatternStore queryPatternStore,
    IQueryPatternTermStore queryPatternTermStore,
    SqliteOptions sqliteOptions,
    OperationalDbOptions operationalDbOptions,
    IngestUseCase ingestUseCase,
    TrainExampleUseCase useCase,
    IConfiguration configuration,
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
            await useCase.TrainAsync(
                request.Question,
                request.SqlText,
                await systemConfigProvider.GetRequiredValueAsync("Retrieval", "Domain", ct),
                isVerified: true,
                ct);

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
        var jobs = await jobStore.GetRecentJobsAsync(100, "Data", ct);
        return Ok(jobs);
    }

    [HttpGet("system-config")]
    public async Task<IActionResult> GetSystemConfig([FromQuery] string? section, CancellationToken ct)
    {
        var profile = await GetActiveSystemConfigProfileAsync(ct);
        if (profile is null)
            return NotFound(new { Error = "No hay perfil activo de SystemConfig." });

        var entries = await systemConfigStore.GetEntriesAsync(profile.Id, ct);
        var filtered = string.IsNullOrWhiteSpace(section)
            ? entries
            : entries.Where(x => string.Equals(x.Section, section, StringComparison.OrdinalIgnoreCase)).ToList();

        return Ok(new
        {
            Profile = profile,
            Entries = filtered
        });
    }

    [HttpPost("system-config")]
    public async Task<IActionResult> UpsertSystemConfigEntry([FromBody] SystemConfigEntryUpsertRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { Error = "Body inválido." });

        if (string.IsNullOrWhiteSpace(request.Section))
            return BadRequest(new { Error = "Section es requerido." });

        if (string.IsNullOrWhiteSpace(request.Key))
            return BadRequest(new { Error = "Key es requerido." });

        if (string.IsNullOrWhiteSpace(request.ValueType))
            return BadRequest(new { Error = "ValueType es requerido." });

        var profile = await GetActiveSystemConfigProfileAsync(ct);
        if (profile is null)
            return NotFound(new { Error = "No hay perfil activo de SystemConfig." });

        var section = request.Section.Trim();
        var key = request.Key.Trim();
        var now = DateTime.UtcNow.ToString("o");
        var existing = await systemConfigStore.GetEntryAsync(profile.Id, section, key, ct);

        var id = await systemConfigStore.UpsertEntryAsync(
            new SystemConfigEntry
            {
                Id = existing?.Id ?? request.Id,
                ProfileId = profile.Id,
                Section = section,
                Key = key,
                Value = request.Value,
                ValueType = request.ValueType.Trim(),
                IsSecret = false,
                SecretRef = null,
                IsEditableInUi = request.IsEditableInUi,
                ValidationRule = string.IsNullOrWhiteSpace(request.ValidationRule) ? null : request.ValidationRule.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                CreatedUtc = existing?.CreatedUtc ?? now,
                UpdatedUtc = now
            },
            ct);

        return Ok(new
        {
            Message = "SystemConfigEntry guardado correctamente.",
            ProfileId = profile.Id,
            Id = id
        });
    }

    [HttpPost("system-config/bulk")]
    public async Task<IActionResult> UpsertSystemConfigEntries([FromBody] SystemConfigBulkUpsertRequest request, CancellationToken ct)
    {
        if (request is null || request.Entries is null || request.Entries.Count == 0)
            return BadRequest(new { Error = "Entries es requerido." });

        var profile = await GetActiveSystemConfigProfileAsync(ct);
        if (profile is null)
            return NotFound(new { Error = "No hay perfil activo de SystemConfig." });

        var now = DateTime.UtcNow.ToString("o");
        var savedIds = new List<int>(request.Entries.Count);

        foreach (var entryRequest in request.Entries)
        {
            if (entryRequest is null)
                return BadRequest(new { Error = "Cada entry debe ser válida." });

            if (string.IsNullOrWhiteSpace(entryRequest.Section))
                return BadRequest(new { Error = "Section es requerido en todas las entries." });

            if (string.IsNullOrWhiteSpace(entryRequest.Key))
                return BadRequest(new { Error = "Key es requerido en todas las entries." });

            if (string.IsNullOrWhiteSpace(entryRequest.ValueType))
                return BadRequest(new { Error = "ValueType es requerido en todas las entries." });

            var section = entryRequest.Section.Trim();
            var key = entryRequest.Key.Trim();
            var existing = await systemConfigStore.GetEntryAsync(profile.Id, section, key, ct);

            var id = await systemConfigStore.UpsertEntryAsync(
                new SystemConfigEntry
                {
                    Id = existing?.Id ?? entryRequest.Id,
                    ProfileId = profile.Id,
                    Section = section,
                    Key = key,
                    Value = entryRequest.Value,
                    ValueType = entryRequest.ValueType.Trim(),
                    IsSecret = false,
                    SecretRef = null,
                    IsEditableInUi = entryRequest.IsEditableInUi,
                    ValidationRule = string.IsNullOrWhiteSpace(entryRequest.ValidationRule) ? null : entryRequest.ValidationRule.Trim(),
                    Description = string.IsNullOrWhiteSpace(entryRequest.Description) ? null : entryRequest.Description.Trim(),
                    CreatedUtc = existing?.CreatedUtc ?? now,
                    UpdatedUtc = now
                },
                ct);

            savedIds.Add(id);
        }

        return Ok(new
        {
            Message = "SystemConfigEntries guardadas correctamente.",
            ProfileId = profile.Id,
            Saved = savedIds.Count,
            Ids = savedIds
        });
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

    // =============================================
    // 6. SEMANTIC HINTS
    // =============================================

    [HttpGet("semantic-hints")]
    public async Task<IActionResult> GetSemanticHints([FromQuery] string domain, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return BadRequest(new { Error = "El parámetro domain es requerido." });

        var hints = await semanticHintStore.GetAllHintsAsync(
            sqliteOptions.DbPath,
            domain,
            ct);

        return Ok(hints);
    }

    [HttpPost("semantic-hints")]
    public async Task<IActionResult> UpsertSemanticHint([FromBody] SemanticHintUpsertRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { Error = "Body inválido." });

        if (string.IsNullOrWhiteSpace(request.Domain))
            return BadRequest(new { Error = "Domain es requerido." });

        if (string.IsNullOrWhiteSpace(request.HintKey))
            return BadRequest(new { Error = "HintKey es requerido." });

        if (string.IsNullOrWhiteSpace(request.HintType))
            return BadRequest(new { Error = "HintType es requerido." });

        if (string.IsNullOrWhiteSpace(request.HintText))
            return BadRequest(new { Error = "HintText es requerido." });

        if (request.Priority < 0)
            return BadRequest(new { Error = "Priority debe ser mayor o igual a 0." });

        var id = await semanticHintStore.UpsertAsync(
            sqliteOptions.DbPath,
            new SemanticHint
            {
                Id = request.Id,
                Domain = request.Domain,
                HintKey = request.HintKey,
                HintType = request.HintType,
                DisplayName = request.DisplayName,
                ObjectName = request.ObjectName,
                ColumnName = request.ColumnName,
                HintText = request.HintText,
                Priority = request.Priority,
                IsActive = request.IsActive
            },
            ct);

        return Ok(new
        {
            Message = "SemanticHint guardada correctamente.",
            Id = id
        });
    }

    [HttpPatch("semantic-hints/{id:long}/status")]
    public async Task<IActionResult> SetSemanticHintStatus(long id, [FromBody] SemanticHintStatusRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { Error = "Body inválido." });

        var updated = await semanticHintStore.SetIsActiveAsync(
            sqliteOptions.DbPath,
            id,
            request.IsActive,
            ct);

        if (!updated)
            return NotFound(new { Error = "SemanticHint no encontrada." });

        return Ok(new
        {
            Message = "Estatus actualizado correctamente.",
            Id = id,
            IsActive = request.IsActive
        });
    }

    // =============================================
    // 7. QUERY PATTERNS
    // =============================================

    [HttpGet("query-patterns")]
    public async Task<IActionResult> GetQueryPatterns([FromQuery] string domain, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return BadRequest(new { Error = "El parámetro domain es requerido." });

        var patterns = await queryPatternStore.GetAllAsync(domain, ct);
        return Ok(patterns);
    }

    [HttpPost("query-patterns")]
    public async Task<IActionResult> UpsertQueryPattern([FromBody] QueryPatternUpsertRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { Error = "Body inválido." });

        if (string.IsNullOrWhiteSpace(request.Domain))
            return BadRequest(new { Error = "Domain es requerido." });

        if (string.IsNullOrWhiteSpace(request.PatternKey))
            return BadRequest(new { Error = "PatternKey es requerido." });

        if (string.IsNullOrWhiteSpace(request.IntentName))
            return BadRequest(new { Error = "IntentName es requerido." });

        if (string.IsNullOrWhiteSpace(request.SqlTemplate))
            return BadRequest(new { Error = "SqlTemplate es requerido." });

        if (request.Priority < 0)
            return BadRequest(new { Error = "Priority debe ser mayor o igual a 0." });

        if (request.DefaultTopN.HasValue && request.DefaultTopN.Value < 0)
            return BadRequest(new { Error = "DefaultTopN debe ser mayor o igual a 0." });

        var id = await queryPatternStore.UpsertAsync(
            new QueryPattern
            {
                Id = request.Id,
                Domain = request.Domain,
                PatternKey = request.PatternKey,
                IntentName = request.IntentName,
                Description = request.Description,
                SqlTemplate = request.SqlTemplate,
                DefaultTopN = request.DefaultTopN,
                MetricKey = request.MetricKey,
                DimensionKey = request.DimensionKey,
                DefaultTimeScopeKey = request.DefaultTimeScopeKey,
                Priority = request.Priority,
                IsActive = request.IsActive
            },
            ct);

        return Ok(new
        {
            Message = "QueryPattern guardado correctamente.",
            Id = id
        });
    }

    [HttpPatch("query-patterns/{id:long}/status")]
    public async Task<IActionResult> SetQueryPatternStatus(long id, [FromBody] QueryPatternStatusRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { Error = "Body inválido." });

        var updated = await queryPatternStore.SetIsActiveAsync(id, request.IsActive, ct);
        if (!updated)
            return NotFound(new { Error = "QueryPattern no encontrado." });

        return Ok(new
        {
            Message = "Estatus actualizado correctamente.",
            Id = id,
            IsActive = request.IsActive
        });
    }

    [HttpGet("query-patterns/{patternId:long}/terms")]
    public async Task<IActionResult> GetQueryPatternTerms(long patternId, CancellationToken ct)
    {
        var pattern = await queryPatternStore.GetByIdAsync(patternId, ct);
        if (pattern is null)
            return NotFound(new { Error = "QueryPattern no encontrado." });

        var terms = await queryPatternTermStore.GetAllByPatternIdAsync(patternId, ct);
        return Ok(terms);
    }

    [HttpPost("query-pattern-terms")]
    public async Task<IActionResult> UpsertQueryPatternTerm([FromBody] QueryPatternTermUpsertRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { Error = "Body inválido." });

        if (request.PatternId <= 0)
            return BadRequest(new { Error = "PatternId es requerido." });

        if (string.IsNullOrWhiteSpace(request.Term))
            return BadRequest(new { Error = "Term es requerido." });

        if (string.IsNullOrWhiteSpace(request.TermGroup))
            return BadRequest(new { Error = "TermGroup es requerido." });

        if (string.IsNullOrWhiteSpace(request.MatchMode))
            return BadRequest(new { Error = "MatchMode es requerido." });

        var pattern = await queryPatternStore.GetByIdAsync(request.PatternId, ct);
        if (pattern is null)
            return NotFound(new { Error = "El QueryPattern asociado no existe." });

        var id = await queryPatternTermStore.UpsertAsync(
            new QueryPatternTerm
            {
                Id = request.Id,
                PatternId = request.PatternId,
                Term = request.Term,
                TermGroup = request.TermGroup,
                MatchMode = request.MatchMode,
                IsRequired = request.IsRequired,
                IsActive = request.IsActive
            },
            ct);

        return Ok(new
        {
            Message = "QueryPatternTerm guardado correctamente.",
            Id = id
        });
    }

    [HttpPatch("query-pattern-terms/{id:long}/status")]
    public async Task<IActionResult> SetQueryPatternTermStatus(long id, [FromBody] QueryPatternTermStatusRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { Error = "Body inválido." });

        var updated = await queryPatternTermStore.SetIsActiveAsync(id, request.IsActive, ct);
        if (!updated)
            return NotFound(new { Error = "QueryPatternTerm no encontrado." });

        return Ok(new
        {
            Message = "Estatus actualizado correctamente.",
            Id = id,
            IsActive = request.IsActive
        });
    }

    private async Task<SystemConfigProfile?> GetActiveSystemConfigProfileAsync(CancellationToken ct)
    {
        var environmentName = configuration["SystemStartup:EnvironmentName"] ?? "Development";
        var defaultProfileKey = configuration["SystemStartup:DefaultSystemProfile"] ?? "default";

        return await systemConfigStore.GetActiveProfileAsync(environmentName, ct)
            ?? await systemConfigStore.GetProfileAsync(environmentName, defaultProfileKey, ct);
    }
}
