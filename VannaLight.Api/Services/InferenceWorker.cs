using Dapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using VannaLight.Api.Hubs;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Settings;
using VannaLight.Core.UseCases;

namespace VannaLight.Api.Services;

public class InferenceWorker(
    IAskRequestQueue queue,
    IServiceScopeFactory scopeFactory,
    IHubContext<AssistantHub> hubContext,
    ILogger<InferenceWorker> logger,
    IOperationalConnectionResolver operationalConnectionResolver,
    SqliteOptions sqliteOptions,
    RuntimeDbOptions runtimeDbOptions) : BackgroundService
{
    private const int SqlExecutionTimeoutSeconds = 8;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("InferenceWorker iniciado. Esperando trabajos (SQL, Docs, Predict)...");

        var memoryDbPath = sqliteOptions.DbPath;
        var runtimeDbPath = runtimeDbOptions.DbPath;

        while (!ct.IsCancellationRequested)
        {
            Guid currentJobId = Guid.Empty;
            string currentConnectionId = string.Empty;
            IJobStore? currentJobStore = null;

            try
            {
                var workItem = await queue.DequeueAsync(ct);

                Guid jobId = (Guid)workItem.JobId;
                string connectionId = workItem.ConnectionId?.ToString() ?? string.Empty;
                currentJobId = jobId;
                currentConnectionId = connectionId;
                string mode = workItem.Mode.ToString();
                string question = workItem.Question.ToString();
                var domain = workItem.Domain.Trim();
                var connectionName = string.IsNullOrWhiteSpace(workItem.ConnectionName)
                    ? string.Empty
                    : workItem.ConnectionName.Trim();
                var systemProfileKey = string.IsNullOrWhiteSpace(workItem.SystemProfileKey)
                    ? "default"
                    : workItem.SystemProfileKey.Trim();

                using var scope = scopeFactory.CreateScope();
                var jobStore = scope.ServiceProvider.GetRequiredService<IJobStore>();
                currentJobStore = jobStore;

                await jobStore.UpdateStatusAsync(jobId, "Analyzing", ct);

                if (!string.IsNullOrWhiteSpace(connectionId))
                {
                    await hubContext.Clients.Client(connectionId)
                        .SendAsync("JobStatusUpdated", new { JobId = jobId, Status = "Analyzing" }, ct);
                }

                // ========================================================
                // CAMINO 1: PREDICCIÓN (ML.NET)
                // ========================================================
                if (mode == "Predict")
                {
                    logger.LogInformation("[Worker] Ejecutando ML.NET para Job {Id}", jobId);

                    var askUseCase = scope.ServiceProvider.GetRequiredService<AskUseCase>();
                    var mlExecutionContext = new VannaLight.Core.Models.AskExecutionContext
                    {
                        TenantKey = string.IsNullOrWhiteSpace(workItem.TenantKey) ? "default" : workItem.TenantKey.Trim(),
                        Domain = domain,
                        ConnectionName = connectionName,
                        SystemProfileKey = systemProfileKey
                    };
                    var mlResult = await askUseCase.PredictAsync(question, mlExecutionContext, ct);

                    if (mlResult.Success)
                    {
                        await jobStore.UpdateJobAsync(
                            jobId,
                            "Completed",
                            mlResult.Sql,
                            mlResult.ResultJson,
                            null,
                            ct);

                        if (!string.IsNullOrWhiteSpace(connectionId))
                        {
                            await hubContext.Clients.Client(connectionId)
                                .SendAsync("JobCompleted", new
                                {
                                    JobId = jobId,
                                    Mode = "Predict",
                                    Explanation = mlResult.Sql,
                                    ResultJson = mlResult.ResultJson
                                }, ct);
                        }
                    }
                    else
                    {
                        await HandleErrorAsync(jobId, connectionId, mlResult.Error, jobStore, ct);
                    }

                    continue;
                }

                // ========================================================
                // CAMINO 2: DOCUMENTOS (RAG PDF)
                // ========================================================
                if (mode == "Docs")
                {
                    logger.LogInformation("[Worker] Ejecutando Búsqueda RAG (Docs) para Job {Id}", jobId);

                    var docsService = scope.ServiceProvider.GetRequiredService<IDocsAnswerService>();
                    var docResult = await docsService.AnswerAsync(question, domain, ct);

                    if (docResult.Success)
                    {
                        var docsPayload = new
                        {
                            answer = docResult.AnswerText,
                            citations = docResult.Citations,
                            confidence = docResult.ConfidenceScore
                        };
                        var jsonCitations = JsonSerializer.Serialize(docsPayload);

                        await jobStore.UpdateJobAsync(
                            jobId,
                            "Completed",
                            docResult.AnswerText,
                            jsonCitations,
                            null,
                            ct);

                        if (!string.IsNullOrWhiteSpace(connectionId))
                        {
                            await hubContext.Clients.Client(connectionId)
                                .SendAsync("JobCompleted", new
                                {
                                    JobId = jobId,
                                    Mode = "Docs",
                                    Explanation = docResult.AnswerText,
                                    ResultJson = jsonCitations
                                }, ct);
                        }
                    }
                    else
                    {
                        await HandleErrorAsync(jobId, connectionId, docResult.ErrorMessage, jobStore, ct);
                    }

                    continue;
                }

                // ========================================================
                // CAMINO 3: DATOS (SQL SERVER)
                // ========================================================
                logger.LogInformation("[Worker] Ejecutando Text-to-SQL para Job {Id}", jobId);
                if (string.IsNullOrWhiteSpace(connectionName))
                {
                    await HandleErrorAsync(
                        jobId,
                        connectionId,
                        "El trabajo no tiene una conexión configurada. Guarda una conexión válida y vuelve a intentarlo.",
                        jobStore,
                        ct);
                    continue;
                }

                var sqlServerConnString = await operationalConnectionResolver.ResolveConnectionStringAsync(connectionName, ct);

                var sqlUseCase = scope.ServiceProvider.GetRequiredService<AskUseCase>();
                var sqlResult = await sqlUseCase.ExecuteAsync(
                    question,
                    memoryDbPath,
                    runtimeDbPath,
                    sqlServerConnString,
                    new VannaLight.Core.Models.AskExecutionContext
                    {
                        TenantKey = workItem.TenantKey,
                        Domain = domain,
                        ConnectionName = connectionName,
                        SystemProfileKey = systemProfileKey
                    },
                    ct);

                if (!sqlResult.Success)
                {
                    if (sqlResult.FailureKind == AskFailureKind.ValidationError ||
                        sqlResult.FailureKind == AskFailureKind.DryRunError)
                    {
                        await jobStore.UpdateJobAsync(
                            jobId,
                            "RequiresReview",
                            sqlResult.Sql,
                            null,
                            sqlResult.Error,
                            ct);

                        await NotifyRequiresReviewAsync(jobId, connectionId, sqlResult.Error, ct);
                    }
                    else
                    {
                        await HandleErrorAsync(jobId, connectionId, sqlResult.Error, jobStore, ct);
                    }

                    continue;
                }

                try
                {
                    await using var connection = new SqlConnection(sqlServerConnString);
                    await connection.OpenAsync(ct);

                    var command = new CommandDefinition(
                        sqlResult.Sql,
                        commandTimeout: SqlExecutionTimeoutSeconds,
                        cancellationToken: ct);

                    var rawRows = await connection.QueryAsync(command);

                    var rows = rawRows
                        .Select(row => ((IDictionary<string, object>)row)
                            .ToDictionary(k => k.Key, v => v.Value))
                        .ToList();

                    string jsonPayload = JsonSerializer.Serialize(rows);

                    await jobStore.UpdateJobAsync(
                        jobId,
                        "Completed",
                        sqlResult.Sql,
                        jsonPayload,
                        null,
                        ct);

                    if (!string.IsNullOrWhiteSpace(connectionId))
                    {
                        await hubContext.Clients.Client(connectionId)
                            .SendAsync("JobCompleted", new
                            {
                                JobId = jobId,
                                Mode = "Data",
                                Sql = sqlResult.Sql,
                                Data = rows
                            }, ct);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error ejecutando SQL para Job {Id}", jobId);

                    await jobStore.UpdateJobAsync(
                        jobId,
                        "Failed",
                        sqlResult.Sql,
                        null,
                        $"Error al ejecutar SQL: {ex.Message}",
                        ct);

                    if (!string.IsNullOrWhiteSpace(connectionId))
                    {
                        await hubContext.Clients.Client(connectionId)
                            .SendAsync("JobFailed", new
                            {
                                JobId = jobId,
                                Error = $"Error al ejecutar SQL: {ex.Message}",
                                Status = "Failed"
                            }, ct);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error crítico procesando inferencia.");

                if (currentJobId != Guid.Empty && currentJobStore is not null)
                {
                    await currentJobStore.SetErrorAsync(
                        currentJobId,
                        $"Error crítico en el worker: {ex.Message}",
                        "Failed",
                        ct);

                    if (!string.IsNullOrWhiteSpace(currentConnectionId))
                    {
                        await hubContext.Clients.Client(currentConnectionId)
                            .SendAsync("JobFailed", new
                            {
                                JobId = currentJobId,
                                Error = $"Error crítico en el worker: {ex.Message}",
                                Status = "Failed"
                            }, ct);
                    }
                }
            }
        }
    }

    private async Task HandleErrorAsync(Guid jobId, string connectionId, string? error, IJobStore store, CancellationToken ct)
    {
        await store.SetErrorAsync(jobId, error ?? "Error desconocido", "Failed", ct);

        if (!string.IsNullOrWhiteSpace(connectionId))
        {
            await hubContext.Clients.Client(connectionId)
                .SendAsync("JobFailed", new
                {
                    JobId = jobId,
                    Error = error,
                    Status = "Failed"
                }, ct);
        }
    }

    private async Task NotifyRequiresReviewAsync(Guid jobId, string connectionId, string? error, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(connectionId))
        {
            await hubContext.Clients.Client(connectionId)
                .SendAsync("JobFailed", new
                {
                    JobId = jobId,
                    Error = error,
                    Status = "RequiresReview"
                }, ct);
        }
    }
}
