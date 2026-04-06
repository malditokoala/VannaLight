using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VannaLight.Api.Contracts;
using VannaLight.Api.Services;
using VannaLight.Api.Services.Predictions;
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

public record TenantUpsertRequest(
    int Id,
    string TenantKey,
    string DisplayName,
    string? Description,
    bool IsActive);

public record TenantDomainUpsertRequest(
    int Id,
    string TenantKey,
    string Domain,
    string ConnectionName,
    string? SystemProfileKey,
    bool IsDefault,
    bool IsActive);

public record OnboardingStep1Request(
    string TenantKey,
    string DisplayName,
    string Domain,
    string ConnectionName,
    string? Description,
    string? SystemProfileKey);

public record OnboardingAllowedObjectItem(
    string SchemaName,
    string ObjectName,
    string ObjectType,
    bool IsSelected);

public record OnboardingAllowedObjectsRequest(
    string Domain,
    IReadOnlyList<OnboardingAllowedObjectItem> Items);

public record OnboardingInitializeRequest(
    string Domain,
    string ConnectionName);

public record ConnectionValidationRequest(
    string ConnectionString);

public record ConnectionProfileUpsertRequest(
    string ConnectionName,
    string ConnectionString,
    string? Description);

public record MlTrainingProfileUpsertRequest(
    string? ProfileName,
    string? DisplayName,
    string? SourceMode,
    string? ConnectionName,
    string? Description,
    string? ShiftTableName,
    string? ProductionViewName,
    string? ScrapViewName,
    string? DowntimeViewName,
    string? TrainingSql);

public record PredictionProfileUpsertRequest(
    long Id,
    string Domain,
    string ProfileKey,
    string DisplayName,
    string DomainPackKey,
    string TargetMetricKey,
    string CalendarProfileKey,
    string Grain,
    int Horizon,
    string HorizonUnit,
    string ModelType,
    string? ConnectionName,
    string? SourceMode,
    string? TargetSeriesSource,
    string? FeatureSourcesJson,
    string? GroupByJson,
    string? FiltersJson,
    string? Notes,
    bool IsActive);

internal sealed record ConnectionValidationMetadata(
    string ServerHost,
    string DatabaseName,
    string? UserName,
    bool IntegratedSecurity,
    bool Encrypt,
    bool TrustServerCertificate,
    string ServerVersion);

[ApiController]
[Route("api/[controller]")]
public class AdminController(
    IJobStore jobStore,
    ISystemConfigProvider systemConfigProvider,
    ISystemConfigStore systemConfigStore,
    IConnectionProfileStore connectionProfileStore,
    IAppSecretStore appSecretStore,
    IOperationalConnectionResolver operationalConnectionResolver,
    IAllowedObjectStore allowedObjectStore,
    ISchemaIngestor schemaIngestor,
    ISchemaStore schemaStore,
    ILlmProfileStore profileStore,
    DocumentIngestor documentIngestor,
    IDocChunkRepository docChunkRepository,
    IBusinessRuleStore businessRuleStore,
    ISemanticHintStore semanticHintStore,
    IQueryPatternStore queryPatternStore,
    IQueryPatternTermStore queryPatternTermStore,
    ITenantStore tenantStore,
    ITenantDomainStore tenantDomainStore,
    ITrainingStore trainingStore,
    IPredictionProfileStore predictionProfileStore,
    SqliteOptions sqliteOptions,
    IMlTrainingProfileProvider mlTrainingProfileProvider,
    IDomainPackProvider domainPackProvider,
    Microsoft.AspNetCore.DataProtection.IDataProtectionProvider dataProtectionProvider,
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
            return BadRequest(new { Error = "Body invÃ¯Â¿Â½lido." });

        if (!Guid.TryParse(request.JobId, out var jobId))
            return BadRequest(new { Error = "JobId invÃ¯Â¿Â½lido." });

        try
        {
            var job = await jobStore.GetJobAsync(jobId, ct);
            if (job is null)
                return NotFound(new { Error = "No se encontrÃ¯Â¿Â½ el job a actualizar en runtime." });

            await useCase.TrainAsync(
                request.Question,
                request.SqlText,
                new AskExecutionContext
                {
                    TenantKey = job.TenantKey,
                    Domain = job.Domain,
                    ConnectionName = job.ConnectionName
                },
                isVerified: true,
                ct);

            var updated = await jobStore.UpdateJobReviewAsync(
                jobId,
                request.SqlText,
                verificationStatus: "Verified",
                comment: request.FeedbackComment,
                ct);

            if (!updated)
                return NotFound(new { Error = "No se encontrÃ¯Â¿Â½ el job a actualizar en runtime." });

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

    [HttpPost("reindex-docs")]
    public async Task<IActionResult> ReindexDocuments(CancellationToken ct)
    {
        var result = await documentIngestor.ReindexAsync(ct);

        return Ok(new
        {
            Message = "Reindex de documentos completado.",
            result.TotalFiles,
            result.Indexed,
            result.Skipped,
            result.Errors
        });
    }

    [HttpPost("reindex-wi")]
    public Task<IActionResult> ReindexWi(CancellationToken ct)
        => ReindexDocuments(ct);

    [HttpGet("documents")]
    public async Task<IActionResult> GetDocuments([FromQuery] string? domain, [FromQuery] int limit = 200, CancellationToken ct = default)
    {
        var docsDomain = string.IsNullOrWhiteSpace(domain)
            ? (configuration["Docs:DefaultDomain"] ?? "work-instructions")
            : domain.Trim();

        var documents = await docChunkRepository.GetDocumentsByDomainAsync(sqliteOptions.DbPath, docsDomain, Math.Clamp(limit, 1, 500), ct);
        return Ok(documents);
    }

    [HttpGet("documents/status")]
    public async Task<IActionResult> GetDocumentsStatus(CancellationToken ct = default)
    {
        var rootPath = await documentIngestor.ResolveDocumentsRootPathAsync(ct);
        var defaultDomain = await documentIngestor.ResolveDocumentsDomainAsync(ct);
        var filesOnDisk = Directory.Exists(rootPath)
            ? Directory.EnumerateFiles(rootPath, "*.pdf", SearchOption.AllDirectories).Count()
            : 0;
        var indexedDocuments = await docChunkRepository.GetDocumentsByDomainAsync(sqliteOptions.DbPath, null, 1000, ct);

        return Ok(new
        {
            RootPath = rootPath,
            DefaultDomain = defaultDomain,
            FilesOnDisk = filesOnDisk,
            IndexedDocuments = indexedDocuments.Count(),
            RootExists = Directory.Exists(rootPath)
        });
    }

    [HttpGet("documents/{docId}/chunks")]
    public async Task<IActionResult> GetDocumentChunks(string docId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(docId))
            return BadRequest(new { Error = "DocId requerido." });

        var chunks = await docChunkRepository.GetDocumentChunksAsync(sqliteOptions.DbPath, docId.Trim(), ct);
        return Ok(chunks);
    }

    [HttpPost("documents/upload")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> UploadDocument([FromForm] IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length <= 0)
            return BadRequest(new { Error = "Adjunta un archivo PDF válido." });

        var saved = await SaveUploadedDocumentsAsync(new[] { file }, ct);

        return Ok(new
        {
            Message = "Documento cargado correctamente.",
            FileName = saved[0].FileName,
            Path = saved[0].Path
        });
    }

    [HttpPost("documents/upload-bulk")]
    [RequestSizeLimit(250_000_000)]
    public async Task<IActionResult> UploadDocumentsBulk([FromForm] List<IFormFile>? files, CancellationToken ct)
    {
        if (files is null || files.Count == 0)
            return BadRequest(new { Error = "Adjunta al menos un archivo PDF v�lido." });

        var results = new List<object>();
        var validFiles = new List<IFormFile>();

        foreach (var file in files)
        {
            if (file is null || file.Length <= 0)
            {
                results.Add(new { FileName = file?.FileName ?? "(vac�o)", Status = "failed", Error = "Archivo vac�o o inv�lido." });
                continue;
            }

            if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new { FileName = file.FileName, Status = "failed", Error = "Solo se permiten archivos PDF." });
                continue;
            }

            validFiles.Add(file);
        }

        if (validFiles.Count > 0)
        {
            var saved = await SaveUploadedDocumentsAsync(validFiles, ct);
            results.AddRange(saved.Select(item => new
            {
                item.FileName,
                Status = "uploaded",
                item.Path
            }));
        }

        var uploaded = results.Count(x => string.Equals(x.GetType().GetProperty("Status")?.GetValue(x)?.ToString(), "uploaded", StringComparison.OrdinalIgnoreCase));
        var failed = results.Count - uploaded;

        return Ok(new
        {
            Message = $"Carga masiva completada. Subidos: {uploaded}. Fallidos: {failed}.",
            Uploaded = uploaded,
            Failed = failed,
            Files = results
        });
    }

    private async Task<List<(string FileName, string Path)>> SaveUploadedDocumentsAsync(IEnumerable<IFormFile> files, CancellationToken ct)
    {
        var rootPath = await documentIngestor.ResolveDocumentsRootPathAsync(ct);
        Directory.CreateDirectory(rootPath);

        var saved = new List<(string FileName, string Path)>();

        foreach (var file in files)
        {
            if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Solo se permiten archivos PDF.");

            var safeFileName = Path.GetFileName(file.FileName);
            var destination = Path.Combine(rootPath, safeFileName);

            await using var stream = System.IO.File.Create(destination);
            await file.CopyToAsync(stream, ct);
            saved.Add((safeFileName, destination));
        }

        return saved;
    }

    [HttpGet("ml/status")]
    public async Task<IActionResult> GetMlStatus(CancellationToken ct = default)
    {
        var profile = await mlTrainingProfileProvider.GetActiveProfileAsync(ct);
        var modelPath = MlModelTrainer.ModelPath;
        var modelExists = System.IO.File.Exists(modelPath);
        var fileInfo = modelExists ? new FileInfo(modelPath) : null;
        var metadata = MlModelTrainer.LoadMetadata();
        var profileAligned = modelExists && MlModelTrainer.IsModelAlignedWithProfile(profile);
        var connectionReady = false;
        string? connectionError = null;

        try
        {
            var connectionString = await mlTrainingProfileProvider.ResolveConnectionStringAsync(profile, ct);
            connectionReady = !string.IsNullOrWhiteSpace(connectionString);
        }
        catch (Exception ex)
        {
            connectionError = ex.Message;
        }

        return Ok(new
        {
            ModelPath = modelPath,
            ModelExists = modelExists,
            ModelSizeBytes = fileInfo?.Length ?? 0,
            ModelLastWriteUtc = fileInfo?.LastWriteTimeUtc,
            ModelDirectory = Path.GetDirectoryName(modelPath),
            ModelDirectoryExists = Directory.Exists(Path.GetDirectoryName(modelPath) ?? string.Empty),
            OperationalConnectionReady = connectionReady,
            ConnectionError = connectionError,
            ModelAlignedWithProfile = profileAligned,
            ModelProfileSignature = metadata?.ProfileSignature,
            ModelTrainedUtc = metadata?.TrainedUtc,
            ProfileName = profile.ProfileName,
            DisplayName = profile.DisplayName,
            SourceMode = profile.NormalizedSourceMode,
            ConnectionName = profile.ConnectionName,
            Description = profile.Description,
            ShiftTableName = profile.ShiftTableQualifiedName,
            ProductionViewName = profile.ProductionViewQualifiedName,
            ScrapViewName = profile.ScrapViewQualifiedName,
            DowntimeViewName = profile.DowntimeViewQualifiedName,
            TrainingSql = profile.TrainingSqlNormalized
        });
    }

    [HttpPost("ml/profile")]
    public async Task<IActionResult> SaveMlProfile([FromBody] MlTrainingProfileUpsertRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { Error = "Body inv�lido." });

        var profile = await GetActiveSystemConfigProfileAsync(ct);
        if (profile is null)
            return NotFound(new { Error = "No hay perfil activo de SystemConfig." });

        var sourceMode = string.Equals(request.SourceMode?.Trim(), MlTrainingProfile.CustomSqlMode, StringComparison.OrdinalIgnoreCase)
            ? MlTrainingProfile.CustomSqlMode
            : MlTrainingProfile.KpiViewsMode;

        if (string.Equals(sourceMode, MlTrainingProfile.CustomSqlMode, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(request.TrainingSql))
        {
            return BadRequest(new { Error = "TrainingSql es requerido cuando SourceMode = CustomSql." });
        }

        var now = DateTime.UtcNow.ToString("O");
        await UpsertSystemConfigValueAsync(profile.Id, "ML", "ProfileName", string.IsNullOrWhiteSpace(request.ProfileName) ? "default-forecast" : request.ProfileName.Trim(), "string", "Identificador del perfil ML activo.", now, ct);
        await UpsertSystemConfigValueAsync(profile.Id, "ML", "DisplayName", string.IsNullOrWhiteSpace(request.DisplayName) ? "Forecasting Profile" : request.DisplayName.Trim(), "string", "Nombre visible del perfil ML.", now, ct);
        await UpsertSystemConfigValueAsync(profile.Id, "ML", "SourceMode", sourceMode, "string", "Modo de fuente del dataset ML.", now, ct);
        await UpsertSystemConfigValueAsync(profile.Id, "ML", "ConnectionName", request.ConnectionName?.Trim() ?? string.Empty, "string", "Nombre l�gico de la conexi�n SQL usada por el entrenamiento ML.", now, ct);
        await UpsertSystemConfigValueAsync(profile.Id, "ML", "Description", request.Description?.Trim() ?? string.Empty, "string", "Descripci�n operativa del perfil ML.", now, ct);
        await UpsertSystemConfigValueAsync(profile.Id, "ML", "ShiftTableName", request.ShiftTableName?.Trim() ?? "dbo.Turnos", "string", "Tabla de turnos usada por el perfil ML cuando SourceMode = KpiViews.", now, ct);
        await UpsertSystemConfigValueAsync(profile.Id, "ML", "ProductionViewName", request.ProductionViewName?.Trim() ?? string.Empty, "string", "Vista de producci�n para el perfil ML.", now, ct);
        await UpsertSystemConfigValueAsync(profile.Id, "ML", "ScrapViewName", request.ScrapViewName?.Trim() ?? string.Empty, "string", "Vista de scrap para el perfil ML.", now, ct);
        await UpsertSystemConfigValueAsync(profile.Id, "ML", "DowntimeViewName", request.DowntimeViewName?.Trim() ?? string.Empty, "string", "Vista de downtime para el perfil ML.", now, ct);
        await UpsertSystemConfigValueAsync(profile.Id, "ML", "TrainingSql", request.TrainingSql?.Trim() ?? string.Empty, "string", "Consulta can�nica para entrenamiento ML cuando SourceMode = CustomSql.", now, ct);

        var savedProfile = await mlTrainingProfileProvider.GetActiveProfileAsync(ct);

        return Ok(new
        {
            Message = "Perfil ML guardado correctamente.",
            ProfileName = savedProfile.ProfileName,
            DisplayName = savedProfile.DisplayName,
            SourceMode = savedProfile.NormalizedSourceMode,
            ConnectionName = savedProfile.ConnectionName,
            Description = savedProfile.Description,
            ShiftTableName = savedProfile.ShiftTableQualifiedName,
            ProductionViewName = savedProfile.ProductionViewQualifiedName,
            ScrapViewName = savedProfile.ScrapViewQualifiedName,
            DowntimeViewName = savedProfile.DowntimeViewQualifiedName,
            TrainingSql = savedProfile.TrainingSqlNormalized
        });
    }

    [HttpPost("ml/train")]
    public async Task<IActionResult> TrainMlModel(CancellationToken ct)
    {
        var profile = await mlTrainingProfileProvider.GetActiveProfileAsync(ct);
        string connectionString;

        try
        {
            connectionString = await mlTrainingProfileProvider.ResolveConnectionStringAsync(profile, ct);
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                Error = $"No hay una conexi�n v�lida para entrenar el modelo ML. {ex.Message}"
            });
        }

        await Task.Run(() => MlModelTrainer.TrainAndSaveModel(connectionString, profile), ct);

        var modelPath = MlModelTrainer.ModelPath;
        var fileInfo = System.IO.File.Exists(modelPath) ? new FileInfo(modelPath) : null;
        var metadata = MlModelTrainer.LoadMetadata();

        return Ok(new
        {
            Message = "Entrenamiento ML completado correctamente.",
            ModelPath = modelPath,
            ModelExists = fileInfo is not null,
            ModelSizeBytes = fileInfo?.Length ?? 0,
            ModelLastWriteUtc = fileInfo?.LastWriteTimeUtc,
            ModelAlignedWithProfile = MlModelTrainer.IsModelAlignedWithProfile(profile),
            ModelProfileSignature = metadata?.ProfileSignature,
            ModelTrainedUtc = metadata?.TrainedUtc,
            ProfileName = profile.ProfileName,
            DisplayName = profile.DisplayName,
            SourceMode = profile.NormalizedSourceMode,
            ConnectionName = profile.ConnectionName,
            ShiftTableName = profile.ShiftTableQualifiedName,
            ProductionViewName = profile.ProductionViewQualifiedName,
            ScrapViewName = profile.ScrapViewQualifiedName,
            DowntimeViewName = profile.DowntimeViewQualifiedName,
            TrainingSql = profile.TrainingSqlNormalized
        });
    }

    [HttpGet("prediction-profiles")]
    public async Task<IActionResult> GetPredictionProfiles([FromQuery] string? domain, CancellationToken ct = default)
    {
        var normalizedDomain = string.IsNullOrWhiteSpace(domain)
            ? (await systemConfigProvider.GetValueAsync("Retrieval", "Domain", ct: ct))?.Trim() ?? "erp-kpi-pilot"
            : domain.Trim();

        var profiles = await predictionProfileStore.GetAllAsync(sqliteOptions.DbPath, normalizedDomain, ct);
        return Ok(profiles);
    }

    [HttpPost("prediction-profiles")]
    public async Task<IActionResult> UpsertPredictionProfile([FromBody] PredictionProfileUpsertRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { Error = "Body inv�lido." });

        if (string.IsNullOrWhiteSpace(request.Domain))
            return BadRequest(new { Error = "Domain es requerido." });

        if (string.IsNullOrWhiteSpace(request.ProfileKey))
            return BadRequest(new { Error = "ProfileKey es requerido." });

        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return BadRequest(new { Error = "DisplayName es requerido." });

        var existing = request.Id > 0
            ? (await predictionProfileStore.GetAllAsync(sqliteOptions.DbPath, request.Domain.Trim(), ct)).FirstOrDefault(x => x.Id == request.Id)
            : await predictionProfileStore.GetAsync(sqliteOptions.DbPath, request.Domain.Trim(), request.ProfileKey.Trim(), ct);

        var now = DateTime.UtcNow.ToString("O");
        var normalizedDomain = request.Domain.Trim();
        var isNorthwindDomain = normalizedDomain.Contains("northwind", StringComparison.OrdinalIgnoreCase);

        var profile = new PredictionProfile
        {
            Id = request.Id > 0 ? request.Id : existing?.Id ?? 0,
            Domain = normalizedDomain,
            ProfileKey = request.ProfileKey.Trim(),
            DisplayName = request.DisplayName.Trim(),
            DomainPackKey = string.IsNullOrWhiteSpace(request.DomainPackKey)
                ? (isNorthwindDomain ? "northwind-sales" : "industrial-kpi")
                : request.DomainPackKey.Trim(),
            TargetMetricKey = string.IsNullOrWhiteSpace(request.TargetMetricKey)
                ? (isNorthwindDomain ? "units_sold" : "scrap_qty")
                : request.TargetMetricKey.Trim(),
            CalendarProfileKey = string.IsNullOrWhiteSpace(request.CalendarProfileKey)
                ? (isNorthwindDomain ? "standard-calendar" : "shift-calendar")
                : request.CalendarProfileKey.Trim(),
            Grain = string.IsNullOrWhiteSpace(request.Grain)
                ? (isNorthwindDomain ? "day" : "day")
                : request.Grain.Trim(),
            Horizon = request.Horizon <= 0 ? (isNorthwindDomain ? 7 : 1) : request.Horizon,
            HorizonUnit = string.IsNullOrWhiteSpace(request.HorizonUnit)
                ? "day"
                : request.HorizonUnit.Trim(),
            ModelType = string.IsNullOrWhiteSpace(request.ModelType) ? "FastTree" : request.ModelType.Trim(),
            ConnectionName = string.IsNullOrWhiteSpace(request.ConnectionName) ? null : request.ConnectionName.Trim(),
            SourceMode = string.IsNullOrWhiteSpace(request.SourceMode)
                ? (isNorthwindDomain ? MlTrainingProfile.CustomSqlMode : MlTrainingProfile.KpiViewsMode)
                : request.SourceMode.Trim(),
            TargetSeriesSource = string.IsNullOrWhiteSpace(request.TargetSeriesSource) ? null : request.TargetSeriesSource.Trim(),
            FeatureSourcesJson = string.IsNullOrWhiteSpace(request.FeatureSourcesJson) ? null : request.FeatureSourcesJson.Trim(),
            GroupByJson = string.IsNullOrWhiteSpace(request.GroupByJson) ? null : request.GroupByJson.Trim(),
            FiltersJson = string.IsNullOrWhiteSpace(request.FiltersJson) ? null : request.FiltersJson.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            IsActive = request.IsActive,
            CreatedUtc = existing?.CreatedUtc ?? now,
            UpdatedUtc = now
        };

        var id = await predictionProfileStore.UpsertAsync(sqliteOptions.DbPath, profile, ct);
        return Ok(new
        {
            Message = "PredictionProfile guardado correctamente.",
            Id = id
        });
    }

    [HttpGet("domain-pack-preview")]
    public async Task<IActionResult> GetDomainPackPreview([FromQuery] string? domain, CancellationToken ct = default)
    {
        var normalizedDomain = string.IsNullOrWhiteSpace(domain)
            ? (await systemConfigProvider.GetValueAsync("Retrieval", "Domain", ct: ct))?.Trim() ?? "erp-kpi-pilot"
            : domain.Trim();

        var pack = await domainPackProvider.GetDomainPackAsync(normalizedDomain, ct);
        return Ok(pack);
    }
    [HttpPost("reindex-schema")]
    public async Task<IActionResult> ReindexSchema(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sqliteOptions.DbPath))
        {
            return BadRequest(new
            {
                Error = "SqliteOptions.DbPath no está configurado."
            });
        }

        try
        {
            var bootstrapConnectionString = await operationalConnectionResolver.ResolveOperationalConnectionStringAsync(ct);
            if (string.IsNullOrWhiteSpace(bootstrapConnectionString))
            {
                return BadRequest(new
                {
                    Error = "No hay una conexión bootstrap configurada para reindexar schema."
                });
            }

            logger.LogInformation(
                "Iniciando reindexación de schema. SqliteDbPath: {SqliteDbPath}",
                sqliteOptions.DbPath);

            await ingestUseCase.ExecuteAsync(
                bootstrapConnectionString,
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
    public async Task<IActionResult> GetHistory([FromQuery] string? tenantKey, [FromQuery] string? domain, [FromQuery] string? connectionName, CancellationToken ct)
    {
        try
        {
            // FIX: Forzamos el modo "Data" (SQL).
            // El Admin de RAG NUNCA debe ver las predicciones de ML.NET.
            var jobs = await jobStore.GetRecentJobsAsync(
                100,
                "Data",
                string.IsNullOrWhiteSpace(tenantKey) ? null : tenantKey.Trim(),
                string.IsNullOrWhiteSpace(domain) ? null : domain.Trim(),
                string.IsNullOrWhiteSpace(connectionName) ? null : connectionName.Trim(),
                ct);
            return Ok(jobs);
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
    }

    [HttpGet("system-config")]
    public async Task<IActionResult> GetSystemConfig([FromQuery] string? section, CancellationToken ct)
    {
        try
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
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
    }

    [HttpGet("onboarding/bootstrap")]
    public async Task<IActionResult> GetOnboardingBootstrap(CancellationToken ct)
    {
        try
        {
            var environmentName = configuration["SystemStartup:EnvironmentName"] ?? "Development";
            var defaultSystemProfile = configuration["SystemStartup:DefaultSystemProfile"] ?? "default";
            var profile = await GetActiveSystemConfigProfileAsync(ct);
            var connections = await connectionProfileStore.GetAllAsync(environmentName, ct);
            var availableConnectionNames = connections
                .Select(x => x.ConnectionName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var tenants = await tenantStore.GetAllAsync(ct);
            var runtimeContexts = new List<object>();
            foreach (var tenant in tenants.Where(t => t.IsActive))
            {
                var mappings = await tenantDomainStore.GetAllByTenantAsync(tenant.TenantKey, ct);
                foreach (var mapping in mappings.Where(m => m.IsActive))
                {
                    if (!availableConnectionNames.Contains(mapping.ConnectionName))
                        continue;

                    runtimeContexts.Add(new
                    {
                        tenantKey = tenant.TenantKey,
                        tenantDisplayName = tenant.DisplayName,
                        domain = mapping.Domain,
                        connectionName = mapping.ConnectionName,
                        systemProfileKey = string.IsNullOrWhiteSpace(mapping.SystemProfileKey) ? "default" : mapping.SystemProfileKey,
                        isDefault = mapping.IsDefault,
                        label = $"{tenant.DisplayName} Ã¯Â¿Â½ {mapping.Domain} Ã¯Â¿Â½ {mapping.ConnectionName}"
                    });
                }
            }

            var defaultTenantKey = (await systemConfigProvider.GetValueAsync("TenantDefaults", "TenantKey", ct: ct))?.Trim();
            var defaultConnectionName = (await systemConfigProvider.GetValueAsync("TenantDefaults", "ConnectionName", ct: ct))?.Trim();
            var defaultDomain = (await systemConfigProvider.GetValueAsync("UiDefaults", "AdminDomain", ct: ct))?.Trim()
                ?? (await systemConfigProvider.GetValueAsync("Retrieval", "Domain", ct: ct))?.Trim();
            if (string.IsNullOrWhiteSpace(defaultConnectionName) || !availableConnectionNames.Contains(defaultConnectionName))
                defaultConnectionName = string.Empty;

            var needsInitialSetup = connections.Count == 0 || runtimeContexts.Count == 0;

            return Ok(new
            {
                EnvironmentName = environmentName,
                Profile = profile,
                NeedsInitialSetup = needsInitialSetup,
                Defaults = new
                {
                    TenantKey = string.IsNullOrWhiteSpace(defaultTenantKey) ? "default" : defaultTenantKey,
                    Domain = string.IsNullOrWhiteSpace(defaultDomain) ? string.Empty : defaultDomain,
                    ConnectionName = string.IsNullOrWhiteSpace(defaultConnectionName) ? string.Empty : defaultConnectionName,
                    SystemProfileKey = defaultSystemProfile
                },
                Tenants = tenants,
                Connections = connections,
                RuntimeContexts = runtimeContexts
            });
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
    }

    [HttpPost("connections/validate")]
    public async Task<IActionResult> ValidateConnectionProfile([FromBody] ConnectionValidationRequest request, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ConnectionString))
            return BadRequest(new { Error = "ConnectionString es requerido." });

        try
        {
            var metadata = await ValidateSqlServerConnectionAsync(request.ConnectionString.Trim(), ct);
            return Ok(new
            {
                Message = "ConexiÃ¯Â¿Â½n validada correctamente.",
                metadata.ServerHost,
                metadata.DatabaseName,
                metadata.UserName,
                metadata.IntegratedSecurity,
                metadata.Encrypt,
                metadata.TrustServerCertificate,
                metadata.ServerVersion
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    [HttpPost("connections")]
    public async Task<IActionResult> SaveConnectionProfile([FromBody] ConnectionProfileUpsertRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { Error = "Body invÃ¯Â¿Â½lido." });

        if (string.IsNullOrWhiteSpace(request.ConnectionName))
            return BadRequest(new { Error = "ConnectionName es requerido." });

        if (string.IsNullOrWhiteSpace(request.ConnectionString))
            return BadRequest(new { Error = "ConnectionString es requerido." });

        var normalizedConnectionName = request.ConnectionName.Trim();
        var environmentName = configuration["SystemStartup:EnvironmentName"] ?? "Development";
        var profileKey = configuration["SystemStartup:DefaultSystemProfile"] ?? "default";
        var now = DateTime.UtcNow.ToString("o");

        ConnectionValidationMetadata metadata;
        try
        {
            metadata = await ValidateSqlServerConnectionAsync(request.ConnectionString.Trim(), ct);
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = $"La conexiÃ¯Â¿Â½n no es vÃ¯Â¿Â½lida: {ex.Message}" });
        }

        var secretKey = BuildConnectionSecretKey(environmentName, profileKey, normalizedConnectionName);
        var protector = dataProtectionProvider.CreateProtector(VannaLight.Infrastructure.Security.AppSecretProtection.Purpose);
        var plainBytes = System.Text.Encoding.UTF8.GetBytes(request.ConnectionString.Trim());
        var cipherBytes = protector.Protect(plainBytes);
        var cipherText = Convert.ToBase64String(cipherBytes);
        var existingSecret = await appSecretStore.GetByKeyAsync(secretKey, ct);

        await appSecretStore.UpsertAsync(
            new AppSecret
            {
                Id = existingSecret?.Id ?? 0,
                SecretKey = secretKey,
                CipherText = cipherText,
                Description = string.IsNullOrWhiteSpace(request.Description)
                    ? $"Connection secret for {normalizedConnectionName}."
                    : request.Description.Trim(),
                CreatedUtc = existingSecret?.CreatedUtc ?? now,
                UpdatedUtc = now
            },
            ct);

        var existingProfile = await connectionProfileStore.GetAsync(environmentName, profileKey, normalizedConnectionName, ct);
        var profileId = await connectionProfileStore.UpsertAsync(
            new ConnectionProfile
            {
                Id = existingProfile?.Id ?? 0,
                EnvironmentName = environmentName,
                ProfileKey = profileKey,
                ConnectionName = normalizedConnectionName,
                ProviderKind = "SqlServer",
                ConnectionMode = "FullStringRef",
                ServerHost = metadata.ServerHost,
                DatabaseName = metadata.DatabaseName,
                UserName = metadata.UserName,
                IntegratedSecurity = metadata.IntegratedSecurity,
                Encrypt = metadata.Encrypt,
                TrustServerCertificate = metadata.TrustServerCertificate,
                CommandTimeoutSec = 30,
                SecretRef = $"appsecret:{secretKey}",
                IsActive = true,
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                CreatedUtc = existingProfile?.CreatedUtc ?? now,
                UpdatedUtc = now
            },
            ct);

        return Ok(new
        {
            Message = "ConexiÃ¯Â¿Â½n guardada correctamente.",
            Id = profileId,
            ConnectionName = normalizedConnectionName,
            metadata.ServerHost,
            metadata.DatabaseName,
            metadata.UserName,
            metadata.IntegratedSecurity,
            metadata.Encrypt,
            metadata.TrustServerCertificate
        });
    }

    [HttpPost("onboarding/step1")]
    public async Task<IActionResult> SaveOnboardingStep1([FromBody] OnboardingStep1Request request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { Error = "Body invÃ¯Â¿Â½lido." });

        if (string.IsNullOrWhiteSpace(request.TenantKey))
            return BadRequest(new { Error = "TenantKey es requerido." });

        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return BadRequest(new { Error = "DisplayName es requerido." });

        if (string.IsNullOrWhiteSpace(request.Domain))
            return BadRequest(new { Error = "Domain es requerido." });

        if (string.IsNullOrWhiteSpace(request.ConnectionName))
            return BadRequest(new { Error = "ConnectionName es requerido." });

        var environmentName = configuration["SystemStartup:EnvironmentName"] ?? "Development";
        var normalizedConnectionName = request.ConnectionName.Trim();
        var connectionProfile = (await connectionProfileStore.GetAllAsync(environmentName, ct))
            .FirstOrDefault(x => string.Equals(x.ConnectionName, normalizedConnectionName, StringComparison.OrdinalIgnoreCase));

        if (connectionProfile is null)
        {
            try
            {
                var resolvedConnectionString = await operationalConnectionResolver.ResolveConnectionStringAsync(normalizedConnectionName, ct);
                if (string.IsNullOrWhiteSpace(resolvedConnectionString))
                    return NotFound(new { Error = "La conexiÃ¯Â¿Â½n seleccionada no existe ni pudo resolverse desde secrets/configuraciÃ¯Â¿Â½n." });
            }
            catch
            {
                return NotFound(new { Error = "La conexiÃ¯Â¿Â½n seleccionada no existe ni pudo resolverse desde secrets/configuraciÃ¯Â¿Â½n." });
            }
        }

        var now = DateTime.UtcNow.ToString("o");
        var normalizedTenantKey = request.TenantKey.Trim();
        var existingTenant = await tenantStore.GetByKeyAsync(normalizedTenantKey, ct);

        var tenantId = await tenantStore.UpsertAsync(
            new Tenant
            {
                Id = existingTenant?.Id ?? 0,
                TenantKey = normalizedTenantKey,
                DisplayName = request.DisplayName.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                IsActive = true,
                CreatedUtc = existingTenant?.CreatedUtc ?? now,
                UpdatedUtc = now
            },
            ct);

        await tenantDomainStore.UpsertAsync(
            new TenantDomain
            {
                Id = 0,
                TenantId = tenantId,
                Domain = request.Domain.Trim(),
                ConnectionName = normalizedConnectionName,
                SystemProfileKey = string.IsNullOrWhiteSpace(request.SystemProfileKey) ? null : request.SystemProfileKey.Trim(),
                IsDefault = true,
                IsActive = true,
                CreatedUtc = now,
                UpdatedUtc = now
            },
            ct);

        return Ok(new
        {
            Message = "Paso 1 del onboarding guardado correctamente.",
            TenantKey = normalizedTenantKey,
            Domain = request.Domain.Trim(),
            ConnectionName = normalizedConnectionName
        });
    }

    [HttpGet("onboarding/schema-candidates")]
    public async Task<IActionResult> GetOnboardingSchemaCandidates(
        [FromQuery] string connectionName,
        [FromQuery] string domain,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(connectionName))
            return BadRequest(new { Error = "El parÃ¯Â¿Â½metro connectionName es requerido." });

        if (string.IsNullOrWhiteSpace(domain))
            return BadRequest(new { Error = "El parÃ¯Â¿Â½metro domain es requerido." });

        try
        {
            var sqlServerConnString = await operationalConnectionResolver.ResolveConnectionStringAsync(connectionName.Trim(), ct);
            var schema = await schemaIngestor.ReadSchemaAsync(sqlServerConnString, ct);
            var existingAllowed = await allowedObjectStore.GetAllObjectsAsync(domain.Trim(), ct);

            var existingLookup = existingAllowed.ToDictionary(
                x => $"{x.SchemaName}.{x.ObjectName}".ToLowerInvariant(),
                x => x);

            var candidates = schema
                .Select(item =>
                {
                    var key = $"{item.Schema}.{item.Name}".ToLowerInvariant();
                    var hasExisting = existingLookup.TryGetValue(key, out var allowed);

                    return new SchemaObjectCandidate
                    {
                        SchemaName = item.Schema,
                        ObjectName = item.Name,
                        ObjectType = InferObjectType(item),
                        Description = item.Description,
                        ColumnCount = item.Columns.Count,
                        PrimaryKeyCount = item.PrimaryKeyColumns.Count,
                        ForeignKeyCount = item.ForeignKeys.Count,
                        IsCurrentlyAllowed = hasExisting && (allowed?.IsActive ?? false),
                        IsSuggested = true
                    };
                })
                .OrderByDescending(x => x.IsCurrentlyAllowed)
                .ThenBy(x => x.SchemaName)
                .ThenBy(x => x.ObjectName)
                .ToList();

            return Ok(candidates);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
    }

    [HttpPost("onboarding/allowed-objects")]
    public async Task<IActionResult> SaveOnboardingAllowedObjects([FromBody] OnboardingAllowedObjectsRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { Error = "Body invÃ¯Â¿Â½lido." });

        if (string.IsNullOrWhiteSpace(request.Domain))
            return BadRequest(new { Error = "Domain es requerido." });

        if (request.Items is null || request.Items.Count == 0)
            return BadRequest(new { Error = "Items es requerido." });

        var normalizedDomain = request.Domain.Trim();
        var existing = await allowedObjectStore.GetAllObjectsAsync(normalizedDomain, ct);
        var incomingLookup = request.Items.ToDictionary(
            x => $"{x.SchemaName.Trim().ToLowerInvariant()}.{x.ObjectName.Trim().ToLowerInvariant()}",
            x => x);

        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.SchemaName) || string.IsNullOrWhiteSpace(item.ObjectName))
                continue;

            await allowedObjectStore.UpsertAsync(
                new AllowedObject
                {
                    Domain = normalizedDomain,
                    SchemaName = item.SchemaName.Trim(),
                    ObjectName = item.ObjectName.Trim(),
                    ObjectType = string.IsNullOrWhiteSpace(item.ObjectType) ? string.Empty : item.ObjectType.Trim(),
                    IsActive = item.IsSelected,
                    Notes = "Seeded from onboarding wizard step 2."
                },
                ct);
        }

        foreach (var item in existing)
        {
            var key = $"{item.SchemaName.ToLowerInvariant()}.{item.ObjectName.ToLowerInvariant()}";
            if (incomingLookup.ContainsKey(key))
                continue;

            if (item.IsActive)
                await allowedObjectStore.SetIsActiveAsync(item.Id, false, ct);
        }

        return Ok(new
        {
            Message = "AllowedObjects del onboarding guardados correctamente.",
            Domain = normalizedDomain,
            SelectedCount = request.Items.Count(x => x.IsSelected)
        });
    }

    [HttpPost("onboarding/initialize")]
    public async Task<IActionResult> InitializeOnboardingDomain([FromBody] OnboardingInitializeRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { Error = "Body invÃ¯Â¿Â½lido." });

        if (string.IsNullOrWhiteSpace(request.Domain))
            return BadRequest(new { Error = "Domain es requerido." });

        if (string.IsNullOrWhiteSpace(request.ConnectionName))
            return BadRequest(new { Error = "ConnectionName es requerido." });

        var normalizedDomain = request.Domain.Trim();
        var normalizedConnectionName = request.ConnectionName.Trim();
        var sqlServerConnString = await operationalConnectionResolver.ResolveConnectionStringAsync(normalizedConnectionName, ct);

        await ingestUseCase.ExecuteAsync(sqlServerConnString, sqliteOptions.DbPath, ct);

        var schema = await schemaIngestor.ReadSchemaAsync(sqlServerConnString, ct);
        var allowedObjects = await allowedObjectStore.GetActiveObjectsAsync(normalizedDomain, ct);
        var allowedLookup = allowedObjects
            .Select(x => $"{x.SchemaName}.{x.ObjectName}".ToLowerInvariant())
            .ToHashSet();

        var seededHints = await SeedSemanticHintsFromSchemaAsync(normalizedDomain, schema, allowedLookup, ct);
        var status = await BuildOnboardingStatusAsync(normalizedDomain, normalizedConnectionName, ct);

        return Ok(new
        {
            Message = "Dominio inicializado correctamente.",
            Domain = normalizedDomain,
            ConnectionName = normalizedConnectionName,
            SeededSemanticHints = seededHints,
            Status = status
        });
    }

    [HttpGet("onboarding/status")]
    public async Task<IActionResult> GetOnboardingStatus([FromQuery] string domain, [FromQuery] string? connectionName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return BadRequest(new { Error = "El parÃ¯Â¿Â½metro domain es requerido." });

        try
        {
            var status = await BuildOnboardingStatusAsync(
                domain.Trim(),
                string.IsNullOrWhiteSpace(connectionName) ? string.Empty : connectionName.Trim(),
                ct);

            return Ok(status);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
    }

    [HttpPost("system-config")]
    public async Task<IActionResult> UpsertSystemConfigEntry([FromBody] SystemConfigEntryUpsertRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { Error = "Body invÃ¯Â¿Â½lido." });

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
                return BadRequest(new { Error = "Cada entry debe ser vÃ¯Â¿Â½lida." });

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

    [HttpGet("tenants")]
    public async Task<IActionResult> GetTenants(CancellationToken ct)
    {
        try
        {
            var tenants = await tenantStore.GetAllAsync(ct);
            return Ok(tenants);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
    }

    [HttpPost("tenants")]
    public async Task<IActionResult> UpsertTenant([FromBody] TenantUpsertRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { Error = "Body invÃ¯Â¿Â½lido." });

        if (string.IsNullOrWhiteSpace(request.TenantKey))
            return BadRequest(new { Error = "TenantKey es requerido." });

        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return BadRequest(new { Error = "DisplayName es requerido." });

        var now = DateTime.UtcNow.ToString("o");
        var existing = await tenantStore.GetByKeyAsync(request.TenantKey.Trim(), ct);

        var id = await tenantStore.UpsertAsync(
            new Tenant
            {
                Id = existing?.Id ?? request.Id,
                TenantKey = request.TenantKey.Trim(),
                DisplayName = request.DisplayName.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                IsActive = request.IsActive,
                CreatedUtc = existing?.CreatedUtc ?? now,
                UpdatedUtc = now
            },
            ct);

        return Ok(new
        {
            Message = "Tenant guardado correctamente.",
            Id = id
        });
    }

    [HttpGet("tenant-domains")]
    public async Task<IActionResult> GetTenantDomains([FromQuery] string tenantKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenantKey))
            return BadRequest(new { Error = "El parÃ¯Â¿Â½metro tenantKey es requerido." });

        try
        {
            var mappings = await tenantDomainStore.GetAllByTenantAsync(tenantKey.Trim(), ct);
            return Ok(mappings);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
    }

    [HttpPost("tenant-domains")]
    public async Task<IActionResult> UpsertTenantDomain([FromBody] TenantDomainUpsertRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { Error = "Body invÃ¯Â¿Â½lido." });

        if (string.IsNullOrWhiteSpace(request.TenantKey))
            return BadRequest(new { Error = "TenantKey es requerido." });

        if (string.IsNullOrWhiteSpace(request.Domain))
            return BadRequest(new { Error = "Domain es requerido." });

        if (string.IsNullOrWhiteSpace(request.ConnectionName))
            return BadRequest(new { Error = "ConnectionName es requerido." });

        var tenant = await tenantStore.GetByKeyAsync(request.TenantKey.Trim(), ct);
        if (tenant is null)
            return NotFound(new { Error = "El Tenant asociado no existe." });

        var now = DateTime.UtcNow.ToString("o");
        var existing = await tenantDomainStore.GetByTenantAndDomainAsync(tenant.TenantKey, request.Domain.Trim(), ct);

        var id = await tenantDomainStore.UpsertAsync(
            new TenantDomain
            {
                Id = existing?.Id ?? request.Id,
                TenantId = tenant.Id,
                Domain = request.Domain.Trim(),
                ConnectionName = request.ConnectionName.Trim(),
                SystemProfileKey = string.IsNullOrWhiteSpace(request.SystemProfileKey) ? null : request.SystemProfileKey.Trim(),
                IsDefault = request.IsDefault,
                IsActive = request.IsActive,
                CreatedUtc = existing?.CreatedUtc ?? now,
                UpdatedUtc = now
            },
            ct);

        return Ok(new
        {
            Message = "TenantDomain guardado correctamente.",
            Id = id
        });
    }

    [HttpGet("domain-pack/export")]
    public async Task<IActionResult> ExportDomainPack([FromQuery] string tenantKey, [FromQuery] string domain, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenantKey))
            return BadRequest(new { Error = "El parÃ¯Â¿Â½metro tenantKey es requerido." });

        if (string.IsNullOrWhiteSpace(domain))
            return BadRequest(new { Error = "El parÃ¯Â¿Â½metro domain es requerido." });

        var normalizedTenantKey = tenantKey.Trim();
        var normalizedDomain = domain.Trim();
        var tenant = await tenantStore.GetByKeyAsync(normalizedTenantKey, ct);
        if (tenant is null)
            return NotFound(new { Error = "El tenant no existe." });

        var mapping = await tenantDomainStore.GetByTenantAndDomainAsync(normalizedTenantKey, normalizedDomain, ct);
        if (mapping is null)
            return NotFound(new { Error = "No existe un mapping TenantDomain para ese tenant y domain." });

        var profile = await GetActiveSystemConfigProfileAsync(ct);
        if (profile is null)
            return NotFound(new { Error = "No hay perfil activo de SystemConfig." });

        var configEntries = (await systemConfigStore.GetEntriesAsync(profile.Id, ct))
            .Where(x => ShouldIncludeInDomainPack(x.Section, x.Key))
            .Select(x => new DomainPackSystemConfigEntryDto(
                x.Section,
                x.Key,
                x.Value,
                x.ValueType,
                x.IsEditableInUi,
                x.ValidationRule,
                x.Description))
            .ToList();

        var allowedObjects = (await allowedObjectStore.GetAllObjectsAsync(normalizedDomain, ct))
            .Select(x => new DomainPackAllowedObjectDto(
                x.SchemaName,
                x.ObjectName,
                x.ObjectType,
                x.IsActive,
                x.Notes))
            .ToList();

        var businessRules = (await businessRuleStore.GetAllRulesAsync(sqliteOptions.DbPath, normalizedDomain, ct))
            .Select(x => new DomainPackBusinessRuleDto(
                x.RuleKey,
                x.RuleText,
                x.Priority,
                x.IsActive))
            .ToList();

        var semanticHints = (await semanticHintStore.GetAllHintsAsync(sqliteOptions.DbPath, normalizedDomain, ct))
            .Select(x => new DomainPackSemanticHintDto(
                x.HintKey,
                x.HintType,
                x.DisplayName,
                x.ObjectName,
                x.ColumnName,
                x.HintText,
                x.Priority,
                x.IsActive))
            .ToList();

        var patterns = await queryPatternStore.GetAllAsync(normalizedDomain, ct);
        var patternDtos = new List<DomainPackQueryPatternDto>(patterns.Count);
        foreach (var pattern in patterns)
        {
            var terms = await queryPatternTermStore.GetAllByPatternIdAsync(pattern.Id, ct);
            patternDtos.Add(new DomainPackQueryPatternDto(
                pattern.PatternKey,
                pattern.IntentName,
                pattern.Description,
                pattern.SqlTemplate,
                pattern.DefaultTopN,
                pattern.MetricKey,
                pattern.DimensionKey,
                pattern.DefaultTimeScopeKey,
                pattern.Priority,
                pattern.IsActive,
                terms.Select(term => new DomainPackQueryPatternTermDto(
                    term.Term,
                    term.TermGroup,
                    term.MatchMode,
                    term.IsRequired,
                    term.IsActive)).ToList()));
        }

        var trainingExamples = (await trainingStore.GetAllTrainingExamplesAsync(sqliteOptions.DbPath, ct))
            .Where(x => string.Equals(x.TenantKey, tenant.TenantKey, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.Equals(x.Domain, normalizedDomain, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.Equals(x.ConnectionName, mapping.ConnectionName, StringComparison.OrdinalIgnoreCase))
            .Select(x => new DomainPackTrainingExampleDto(
                x.Question,
                x.Sql,
                x.IntentName,
                x.IsVerified,
                x.Priority))
            .ToList();

        var pack = new DomainPackDto(
            Version: "1.0",
            ExportedUtc: DateTime.UtcNow.ToString("o"),
            TenantKey: tenant.TenantKey,
            TenantDisplayName: tenant.DisplayName,
            TenantDescription: tenant.Description,
            Domain: normalizedDomain,
            ConnectionName: mapping.ConnectionName,
            SystemProfileKey: mapping.SystemProfileKey,
            SystemConfigEntries: configEntries,
            AllowedObjects: allowedObjects,
            BusinessRules: businessRules,
            SemanticHints: semanticHints,
            QueryPatterns: patternDtos,
            TrainingExamples: trainingExamples);

        return Ok(pack);
    }

    [HttpPost("domain-pack/import")]
    public async Task<IActionResult> ImportDomainPack([FromBody] DomainPackDto pack, CancellationToken ct)
    {
        if (pack is null)
            return BadRequest(new { Error = "Body invÃ¯Â¿Â½lido." });

        if (string.IsNullOrWhiteSpace(pack.TenantKey))
            return BadRequest(new { Error = "TenantKey es requerido en el pack." });

        if (string.IsNullOrWhiteSpace(pack.TenantDisplayName))
            return BadRequest(new { Error = "TenantDisplayName es requerido en el pack." });

        if (string.IsNullOrWhiteSpace(pack.Domain))
            return BadRequest(new { Error = "Domain es requerido en el pack." });

        if (string.IsNullOrWhiteSpace(pack.ConnectionName))
            return BadRequest(new { Error = "ConnectionName es requerido en el pack." });

        var normalizedTenantKey = pack.TenantKey.Trim();
        var normalizedDomain = pack.Domain.Trim();
        var normalizedConnectionName = pack.ConnectionName.Trim();
        var now = DateTime.UtcNow.ToString("o");

        var existingTenant = await tenantStore.GetByKeyAsync(normalizedTenantKey, ct);
        var tenantId = await tenantStore.UpsertAsync(
            new Tenant
            {
                Id = existingTenant?.Id ?? 0,
                TenantKey = normalizedTenantKey,
                DisplayName = pack.TenantDisplayName.Trim(),
                Description = string.IsNullOrWhiteSpace(pack.TenantDescription) ? null : pack.TenantDescription.Trim(),
                IsActive = true,
                CreatedUtc = existingTenant?.CreatedUtc ?? now,
                UpdatedUtc = now
            },
            ct);

        await tenantDomainStore.UpsertAsync(
            new TenantDomain
            {
                Id = 0,
                TenantId = tenantId,
                Domain = normalizedDomain,
                ConnectionName = normalizedConnectionName,
                SystemProfileKey = string.IsNullOrWhiteSpace(pack.SystemProfileKey) ? null : pack.SystemProfileKey.Trim(),
                IsDefault = true,
                IsActive = true,
                CreatedUtc = now,
                UpdatedUtc = now
            },
            ct);

        var profile = await GetActiveSystemConfigProfileAsync(ct);
        if (profile is null)
            return NotFound(new { Error = "No hay perfil activo de SystemConfig." });

        foreach (var entry in pack.SystemConfigEntries ?? Array.Empty<DomainPackSystemConfigEntryDto>())
        {
            if (string.IsNullOrWhiteSpace(entry.Section) || string.IsNullOrWhiteSpace(entry.Key) || string.IsNullOrWhiteSpace(entry.ValueType))
                continue;

            var section = entry.Section.Trim();
            var key = entry.Key.Trim();
            var existing = await systemConfigStore.GetEntryAsync(profile.Id, section, key, ct);

            await systemConfigStore.UpsertEntryAsync(
                new SystemConfigEntry
                {
                    Id = existing?.Id ?? 0,
                    ProfileId = profile.Id,
                    Section = section,
                    Key = key,
                    Value = entry.Value,
                    ValueType = entry.ValueType.Trim(),
                    IsSecret = false,
                    SecretRef = null,
                    IsEditableInUi = entry.IsEditableInUi,
                    ValidationRule = string.IsNullOrWhiteSpace(entry.ValidationRule) ? null : entry.ValidationRule.Trim(),
                    Description = string.IsNullOrWhiteSpace(entry.Description) ? null : entry.Description.Trim(),
                    CreatedUtc = existing?.CreatedUtc ?? now,
                    UpdatedUtc = now
                },
                ct);
        }

        foreach (var item in pack.AllowedObjects ?? Array.Empty<DomainPackAllowedObjectDto>())
        {
            if (string.IsNullOrWhiteSpace(item.SchemaName) || string.IsNullOrWhiteSpace(item.ObjectName))
                continue;

            await allowedObjectStore.UpsertAsync(
                new AllowedObject
                {
                    Domain = normalizedDomain,
                    SchemaName = item.SchemaName.Trim(),
                    ObjectName = item.ObjectName.Trim(),
                    ObjectType = string.IsNullOrWhiteSpace(item.ObjectType) ? string.Empty : item.ObjectType.Trim(),
                    IsActive = item.IsActive,
                    Notes = string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes.Trim()
                },
                ct);
        }

        foreach (var rule in pack.BusinessRules ?? Array.Empty<DomainPackBusinessRuleDto>())
        {
            if (string.IsNullOrWhiteSpace(rule.RuleKey) || string.IsNullOrWhiteSpace(rule.RuleText))
                continue;

            await businessRuleStore.UpsertAsync(
                sqliteOptions.DbPath,
                new BusinessRule
                {
                    Domain = normalizedDomain,
                    RuleKey = rule.RuleKey.Trim(),
                    RuleText = rule.RuleText.Trim(),
                    Priority = rule.Priority,
                    IsActive = rule.IsActive
                },
                ct);
        }

        foreach (var hint in pack.SemanticHints ?? Array.Empty<DomainPackSemanticHintDto>())
        {
            if (string.IsNullOrWhiteSpace(hint.HintKey) || string.IsNullOrWhiteSpace(hint.HintType) || string.IsNullOrWhiteSpace(hint.HintText))
                continue;

            await semanticHintStore.UpsertAsync(
                sqliteOptions.DbPath,
                new SemanticHint
                {
                    Domain = normalizedDomain,
                    HintKey = hint.HintKey.Trim(),
                    HintType = hint.HintType.Trim(),
                    DisplayName = string.IsNullOrWhiteSpace(hint.DisplayName) ? null : hint.DisplayName.Trim(),
                    ObjectName = string.IsNullOrWhiteSpace(hint.ObjectName) ? null : hint.ObjectName.Trim(),
                    ColumnName = string.IsNullOrWhiteSpace(hint.ColumnName) ? null : hint.ColumnName.Trim(),
                    HintText = hint.HintText.Trim(),
                    Priority = hint.Priority,
                    IsActive = hint.IsActive
                },
                ct);
        }

        var existingPatterns = await queryPatternStore.GetAllAsync(normalizedDomain, ct);
        foreach (var pattern in pack.QueryPatterns ?? Array.Empty<DomainPackQueryPatternDto>())
        {
            if (string.IsNullOrWhiteSpace(pattern.PatternKey) || string.IsNullOrWhiteSpace(pattern.IntentName) || string.IsNullOrWhiteSpace(pattern.SqlTemplate))
                continue;

            var patternKey = pattern.PatternKey.Trim();
            var existingPattern = existingPatterns.FirstOrDefault(x =>
                string.Equals(x.PatternKey, patternKey, StringComparison.OrdinalIgnoreCase));

            var patternId = await queryPatternStore.UpsertAsync(
                new QueryPattern
                {
                    Id = existingPattern?.Id ?? 0,
                    Domain = normalizedDomain,
                    PatternKey = patternKey,
                    IntentName = pattern.IntentName.Trim(),
                    Description = string.IsNullOrWhiteSpace(pattern.Description) ? null : pattern.Description.Trim(),
                    SqlTemplate = pattern.SqlTemplate,
                    DefaultTopN = pattern.DefaultTopN,
                    MetricKey = string.IsNullOrWhiteSpace(pattern.MetricKey) ? null : pattern.MetricKey.Trim(),
                    DimensionKey = string.IsNullOrWhiteSpace(pattern.DimensionKey) ? null : pattern.DimensionKey.Trim(),
                    DefaultTimeScopeKey = string.IsNullOrWhiteSpace(pattern.DefaultTimeScopeKey) ? null : pattern.DefaultTimeScopeKey.Trim(),
                    Priority = pattern.Priority,
                    IsActive = pattern.IsActive,
                    CreatedUtc = existingPattern?.CreatedUtc ?? DateTime.UtcNow,
                    UpdatedUtc = DateTime.UtcNow
                },
                ct);

            var existingTerms = await queryPatternTermStore.GetAllByPatternIdAsync(patternId, ct);
            foreach (var term in pattern.Terms ?? Array.Empty<DomainPackQueryPatternTermDto>())
            {
                if (string.IsNullOrWhiteSpace(term.Term) || string.IsNullOrWhiteSpace(term.TermGroup) || string.IsNullOrWhiteSpace(term.MatchMode))
                    continue;

                var existingTerm = existingTerms.FirstOrDefault(x =>
                    string.Equals(x.Term, term.Term.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.TermGroup, term.TermGroup.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.MatchMode, term.MatchMode.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    x.IsRequired == term.IsRequired);

                await queryPatternTermStore.UpsertAsync(
                    new QueryPatternTerm
                    {
                        Id = existingTerm?.Id ?? 0,
                        PatternId = patternId,
                        Term = term.Term.Trim(),
                        TermGroup = term.TermGroup.Trim(),
                        MatchMode = term.MatchMode.Trim(),
                        IsRequired = term.IsRequired,
                        IsActive = term.IsActive,
                        CreatedUtc = existingTerm?.CreatedUtc ?? DateTime.UtcNow
                    },
                    ct);
            }
        }

        foreach (var example in pack.TrainingExamples ?? Array.Empty<DomainPackTrainingExampleDto>())
        {
            if (string.IsNullOrWhiteSpace(example.Question) || string.IsNullOrWhiteSpace(example.Sql))
                continue;

            await trainingStore.UpsertAsync(
                new TrainingExampleUpsert(
                    example.Question.Trim(),
                    example.Sql,
                    normalizedTenantKey,
                    normalizedDomain,
                    normalizedConnectionName,
                    string.IsNullOrWhiteSpace(example.IntentName) ? null : example.IntentName.Trim(),
                    example.IsVerified,
                    example.Priority),
                ct);
        }

        return Ok(new
        {
            Message = "Domain pack importado correctamente.",
            TenantKey = normalizedTenantKey,
            Domain = normalizedDomain,
            ConnectionName = normalizedConnectionName,
            Imported = new
            {
                SystemConfigEntries = pack.SystemConfigEntries?.Count ?? 0,
                AllowedObjects = pack.AllowedObjects?.Count ?? 0,
                BusinessRules = pack.BusinessRules?.Count ?? 0,
                SemanticHints = pack.SemanticHints?.Count ?? 0,
                QueryPatterns = pack.QueryPatterns?.Count ?? 0,
                TrainingExamples = pack.TrainingExamples?.Count ?? 0
            }
        });
    }

    // ==========================================
    // 3. GESTIÃ¯Â¿Â½N DE PERFILES LLM (SLIM)
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
            return BadRequest(new { Error = "Body invÃ¯Â¿Â½lido." });

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
            return BadRequest(new { Error = "El parÃ¯Â¿Â½metro domain es requerido." });

        try
        {
            var items = await allowedObjectStore.GetAllObjectsAsync(domain, ct);
            return Ok(items);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
    }

    [HttpPost("allowed-objects")]
    public async Task<IActionResult> UpsertAllowedObject([FromBody] AllowedObjectUpsertRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { Error = "Body invÃ¯Â¿Â½lido." });

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
            return BadRequest(new { Error = "Body invÃ¯Â¿Â½lido." });

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
            return BadRequest(new { Error = "El parÃ¯Â¿Â½metro domain es requerido." });

        try
        {
            var rules = await businessRuleStore.GetAllRulesAsync(
                sqliteOptions.DbPath,
                domain,
                ct);

            return Ok(rules);
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
    }

    [HttpPost("business-rules")]
    public async Task<IActionResult> UpsertBusinessRule([FromBody] BusinessRuleUpsertRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { Error = "Body invÃ¯Â¿Â½lido." });

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
            return BadRequest(new { Error = "Body invÃ¯Â¿Â½lido." });

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
            return BadRequest(new { Error = "El parÃ¯Â¿Â½metro domain es requerido." });

        try
        {
            var hints = await semanticHintStore.GetAllHintsAsync(
                sqliteOptions.DbPath,
                domain,
                ct);

            return Ok(hints);
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
    }

    [HttpPost("semantic-hints")]
    public async Task<IActionResult> UpsertSemanticHint([FromBody] SemanticHintUpsertRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { Error = "Body invÃ¯Â¿Â½lido." });

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
            return BadRequest(new { Error = "Body invÃ¯Â¿Â½lido." });

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
            return BadRequest(new { Error = "El parÃ¯Â¿Â½metro domain es requerido." });

        try
        {
            var patterns = await queryPatternStore.GetAllAsync(domain, ct);
            return Ok(patterns);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
    }

    [HttpPost("query-patterns")]
    public async Task<IActionResult> UpsertQueryPattern([FromBody] QueryPatternUpsertRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { Error = "Body invÃ¯Â¿Â½lido." });

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
            return BadRequest(new { Error = "Body invÃ¯Â¿Â½lido." });

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
        try
        {
            var pattern = await queryPatternStore.GetByIdAsync(patternId, ct);
            if (pattern is null)
                return NotFound(new { Error = "QueryPattern no encontrado." });

            var terms = await queryPatternTermStore.GetAllByPatternIdAsync(patternId, ct);
            return Ok(terms);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
    }

    [HttpPost("query-pattern-terms")]
    public async Task<IActionResult> UpsertQueryPatternTerm([FromBody] QueryPatternTermUpsertRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { Error = "Body invÃ¯Â¿Â½lido." });

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
            return BadRequest(new { Error = "Body invÃ¯Â¿Â½lido." });

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

    private async Task UpsertSystemConfigValueAsync(
        int profileId,
        string section,
        string key,
        string value,
        string valueType,
        string description,
        string now,
        CancellationToken ct)
    {
        var existing = await systemConfigStore.GetEntryAsync(profileId, section, key, ct);

        await systemConfigStore.UpsertEntryAsync(
            new SystemConfigEntry
            {
                Id = existing?.Id ?? 0,
                ProfileId = profileId,
                Section = section,
                Key = key,
                Value = value,
                ValueType = valueType,
                IsSecret = false,
                SecretRef = null,
                IsEditableInUi = true,
                ValidationRule = null,
                Description = description,
                CreatedUtc = existing?.CreatedUtc ?? now,
                UpdatedUtc = now
            },
            ct);
    }

    private static string InferObjectType(TableSchema schema)
    {
        return schema.PrimaryKeyColumns.Count > 0 ? "TABLE" : "VIEW";
    }

    private static bool ShouldIncludeInDomainPack(string section, string key)
    {
        if (string.IsNullOrWhiteSpace(section) || string.IsNullOrWhiteSpace(key))
            return false;

        if (section.Equals("Prompting", StringComparison.OrdinalIgnoreCase))
            return true;

        if (section.Equals("Retrieval", StringComparison.OrdinalIgnoreCase))
            return true;

        if (section.Equals("UiDefaults", StringComparison.OrdinalIgnoreCase))
            return key.Equals("AdminDomain", StringComparison.OrdinalIgnoreCase)
                || key.Equals("AdminTenant", StringComparison.OrdinalIgnoreCase);

        if (section.Equals("TenantDefaults", StringComparison.OrdinalIgnoreCase))
            return key.Equals("TenantKey", StringComparison.OrdinalIgnoreCase)
                || key.Equals("ConnectionName", StringComparison.OrdinalIgnoreCase);

        return false;
    }

    private async Task<object> BuildOnboardingStatusAsync(string domain, string connectionName, CancellationToken ct)
    {
        var allowedObjects = await allowedObjectStore.GetActiveObjectsAsync(domain, ct);
        var semanticHints = await semanticHintStore.GetAllHintsAsync(sqliteOptions.DbPath, domain, ct);
        var schemaDocs = await schemaStore.GetAllSchemaDocsAsync(sqliteOptions.DbPath, ct);

        var allowedLookup = allowedObjects
            .Select(x => $"{x.SchemaName}.{x.ObjectName}".ToLowerInvariant())
            .ToHashSet();

        var matchingSchemaDocs = schemaDocs.Count(doc =>
            allowedLookup.Contains($"{doc.Schema}.{doc.Table}".ToLowerInvariant()));

        return new
        {
            Domain = domain,
            ConnectionName = connectionName,
            AllowedObjectsCount = allowedObjects.Count,
            SchemaDocsCount = matchingSchemaDocs,
            SemanticHintsCount = semanticHints.Count(x => x.IsActive),
            Health = new
            {
                HasAllowedObjects = allowedObjects.Count > 0,
                HasSchemaDocs = matchingSchemaDocs > 0,
                HasSemanticHints = semanticHints.Any(x => x.IsActive)
            }
        };
    }

    private async Task<int> SeedSemanticHintsFromSchemaAsync(
        string domain,
        IReadOnlyList<TableSchema> schema,
        IReadOnlySet<string> allowedLookup,
        CancellationToken ct)
    {
        var seeded = 0;

        foreach (var table in schema)
        {
            var objectKey = $"{table.Schema}.{table.Name}".ToLowerInvariant();
            if (!allowedLookup.Contains(objectKey))
                continue;

            seeded += await UpsertSemanticHintSeedAsync(
                new SemanticHint
                {
                    Domain = domain,
                    HintKey = $"entity:{table.Schema}.{table.Name}".ToLowerInvariant(),
                    HintType = "entity",
                    DisplayName = table.Name,
                    ObjectName = $"{table.Schema}.{table.Name}",
                    ColumnName = null,
                    HintText = $"Entidad principal disponible para consultas sobre {table.Schema}.{table.Name}.",
                    Priority = 50,
                    IsActive = true
                },
                ct);

            foreach (var column in table.Columns)
            {
                var hintType = InferHintType(column);
                if (hintType is null)
                    continue;

                seeded += await UpsertSemanticHintSeedAsync(
                    new SemanticHint
                    {
                        Domain = domain,
                        HintKey = $"{hintType}:{table.Schema}.{table.Name}.{column.Name}".ToLowerInvariant(),
                        HintType = hintType,
                        DisplayName = column.Name,
                        ObjectName = $"{table.Schema}.{table.Name}",
                        ColumnName = column.Name,
                        HintText = BuildHintText(hintType, table, column),
                        Priority = hintType == "time_field" ? 60 : hintType == "measure" ? 70 : 80,
                        IsActive = true
                    },
                    ct);
            }
        }

        return seeded;
    }

    private async Task<int> UpsertSemanticHintSeedAsync(SemanticHint hint, CancellationToken ct)
    {
        var id = await semanticHintStore.UpsertAsync(sqliteOptions.DbPath, hint, ct);
        return id > 0 ? 1 : 0;
    }

    private static string? InferHintType(ColumnSchema column)
    {
        var normalizedName = column.Name.Trim().ToLowerInvariant();
        var normalizedType = column.SqlType.Trim().ToLowerInvariant();

        if (normalizedName.Contains("date") || normalizedName.Contains("time") || normalizedName.Contains("yearmonth") || normalizedName.Contains("week") || normalizedName.Contains("shift"))
            return "time_field";

        var isNumeric =
            normalizedType.Contains("int") ||
            normalizedType.Contains("decimal") ||
            normalizedType.Contains("numeric") ||
            normalizedType.Contains("money") ||
            normalizedType.Contains("float") ||
            normalizedType.Contains("real");

        if (isNumeric && (normalizedName.Contains("qty") || normalizedName.Contains("count") || normalizedName.Contains("amount") || normalizedName.Contains("total") || normalizedName.Contains("cost") || normalizedName.Contains("price") || normalizedName.Contains("value") || normalizedName.Contains("minutes") || normalizedName.Contains("hours")))
            return "measure";

        if (normalizedName.EndsWith("name") || normalizedName.EndsWith("number") || normalizedName.EndsWith("code") || normalizedName.Contains("type") || normalizedName.Contains("status") || normalizedName.Contains("category"))
            return "dimension";

        return null;
    }

    private static string BuildHintText(string hintType, TableSchema table, ColumnSchema column)
    {
        return hintType switch
        {
            "time_field" => $"Campo temporal sugerido para filtros o agrupaciones: {table.Schema}.{table.Name}.{column.Name}.",
            "measure" => $"MÃ¯Â¿Â½trica cuantitativa sugerida para agregaciones: {table.Schema}.{table.Name}.{column.Name}.",
            "dimension" => $"DimensiÃ¯Â¿Â½n o etiqueta descriptiva sugerida para segmentar resultados: {table.Schema}.{table.Name}.{column.Name}.",
            _ => $"Pista semÃ¯Â¿Â½ntica generada para {table.Schema}.{table.Name}.{column.Name}."
        };
    }

    private static string BuildConnectionSecretKey(string environmentName, string profileKey, string connectionName)
    {
        return $"connections/{environmentName.Trim().ToLowerInvariant()}/{profileKey.Trim().ToLowerInvariant()}/{connectionName.Trim().ToLowerInvariant()}";
    }

    private static async Task<ConnectionValidationMetadata> ValidateSqlServerConnectionAsync(string connectionString, CancellationToken ct)
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString)
        {
            ConnectTimeout = 5
        };

        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128));";
        var serverVersion = Convert.ToString(await command.ExecuteScalarAsync(ct)) ?? connection.ServerVersion;

        return new ConnectionValidationMetadata(
            ServerHost: builder.DataSource,
            DatabaseName: builder.InitialCatalog,
            UserName: string.IsNullOrWhiteSpace(builder.UserID) ? null : builder.UserID,
            IntegratedSecurity: builder.IntegratedSecurity,
            Encrypt: builder.Encrypt,
            TrustServerCertificate: builder.TrustServerCertificate,
            ServerVersion: serverVersion);
    }
}








