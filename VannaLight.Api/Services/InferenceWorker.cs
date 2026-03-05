using Dapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using VannaLight.Api.Contracts;          // ✅ AskMode único
using VannaLight.Api.Data;
using VannaLight.Api.Hubs;
using VannaLight.Core.UseCases;

namespace VannaLight.Api.Services;

public class InferenceWorker(
    IAskRequestQueue queue,
    IServiceScopeFactory scopeFactory,
    IHubContext<AssistantHub> hubContext,
    ILogger<InferenceWorker> logger,
    IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("InferenceWorker iniciado. Esperando trabajos...");

        string sqlitePath = configuration["Paths:Sqlite"] ?? "vanna_memory.db";
        string sqlServerConnString = configuration.GetConnectionString("OperationalDb")
            ?? throw new InvalidOperationException("Falta la cadena de conexión en el appsettings.");

        while (!ct.IsCancellationRequested)
        {
            AskWorkItem? workItem = null;

            try
            {
                // 1. Sacamos el trabajo de la cola
                workItem = await queue.DequeueAsync(ct);

                // ✅ Debug visible para confirmar que el Mode viaja bien
                logger.LogInformation("[Worker] Job={JobId} Mode={Mode} Question={Question}",
                    workItem.JobId, workItem.Mode, workItem.Question);

                // 2. AISLAMOS EL TRABAJO: Si este Job específico falla, lo cachamos aquí adentro.
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var jobStore = scope.ServiceProvider.GetRequiredService<IJobStore>();

                    await jobStore.UpdateStatusAsync(workItem.JobId, "Analyzing");
                    if (!string.IsNullOrWhiteSpace(workItem.ConnectionId))
                    {
                        await hubContext.Clients.Client(workItem.ConnectionId)
                            .SendAsync("JobStatusUpdated", new { workItem.JobId, Status = "Analyzing" }, ct);
                    }

                    // ==========================================================
                    // ✅ FASE 3: ROUTING POR MODO (Docs / Predict / Data)
                    // ==========================================================

                    // 1) DOCS MODE (NO SQL SERVER)
                    if (workItem.Mode == AskMode.Docs)
                    {
                        var docsService = scope.ServiceProvider.GetRequiredService<DocsAnswerService>();
                        var docsResult = await docsService.AnswerAsync(workItem.Question, ct);

                        if (!docsResult.Success)
                        {
                            await jobStore.SetErrorAsync(workItem.JobId, docsResult.Error ?? "Docs: sin evidencia.", "Failed");

                            if (!string.IsNullOrWhiteSpace(workItem.ConnectionId))
                            {
                                await hubContext.Clients.Client(workItem.ConnectionId)
                                    .SendAsync("JobFailed", new { workItem.JobId, Error = docsResult.Error }, ct);
                            }
                            continue;
                        }

                        // Por ahora guardamos Answer en SqlText (rápido). Luego lo movemos a AnswerText/ResultJson.
                        await jobStore.SetResultAsync(workItem.JobId, docsResult.Answer ?? "");

                        if (!string.IsNullOrWhiteSpace(workItem.ConnectionId))
                        {
                            await hubContext.Clients.Client(workItem.ConnectionId)
                                .SendAsync("JobCompleted", new
                                {
                                    workItem.JobId,
                                    Mode = "Docs",
                                    Answer = docsResult.Answer,
                                    Citations = docsResult.Citations
                                }, ct);
                        }

                        continue;
                    }

                    // 2) PREDICT MODE (STUB)
                    if (workItem.Mode == AskMode.Predict)
                    {
                        await jobStore.SetErrorAsync(workItem.JobId, "Predict mode aún no disponible (Fase 3 - stub).", "Failed");

                        if (!string.IsNullOrWhiteSpace(workItem.ConnectionId))
                        {
                            await hubContext.Clients.Client(workItem.ConnectionId)
                                .SendAsync("JobFailed", new { workItem.JobId, Error = "Predict mode aún no disponible." }, ct);
                        }

                        continue;
                    }

                    // 3) DATA MODE (SQL) - flujo actual
                    var askUseCase = scope.ServiceProvider.GetRequiredService<AskUseCase>();

                    var result = await askUseCase.ExecuteAsync(
                        workItem.Question,
                        sqlitePath,
                        sqlServerConnString,
                        ct);

                    if (result.Success)
                    {
                        IEnumerable<dynamic> queryResults = Array.Empty<dynamic>();
                        string? executionError = null;

                        try
                        {
                            using var connection = new SqlConnection(sqlServerConnString);
                            queryResults = await connection.QueryAsync(result.Sql);
                        }
                        catch (Exception ex)
                        {
                            executionError = ex.Message;
                        }

                        if (executionError == null)
                        {
                            await jobStore.SetResultAsync(workItem.JobId, result.Sql);

                            if (!string.IsNullOrWhiteSpace(workItem.ConnectionId))
                            {
                                await hubContext.Clients.Client(workItem.ConnectionId)
                                    .SendAsync("JobCompleted", new
                                    {
                                        workItem.JobId,
                                        Mode = "Data",
                                        Sql = result.Sql,
                                        Data = queryResults
                                    }, ct);
                            }
                        }
                        else
                        {
                            await jobStore.SetErrorAsync(workItem.JobId, $"Error de ejecución: {executionError}", "Failed");

                            if (!string.IsNullOrWhiteSpace(workItem.ConnectionId))
                            {
                                await hubContext.Clients.Client(workItem.ConnectionId)
                                    .SendAsync("JobFailed", new
                                    {
                                        workItem.JobId,
                                        Error = $"El SQL es válido pero falló al ejecutarse: {executionError}"
                                    }, ct);
                            }
                        }
                    }
                    else
                    {
                        await jobStore.SetErrorAsync(workItem.JobId, result.Error ?? "Error desconocido", "Failed");

                        if (!string.IsNullOrWhiteSpace(workItem.ConnectionId))
                        {
                            await hubContext.Clients.Client(workItem.ConnectionId)
                                .SendAsync("JobFailed", new { workItem.JobId, Error = result.Error }, ct);
                        }
                    }
                }
                catch (Exception jobEx)
                {
                    // 3. AQUÍ MATAMOS AL ZOMBIE 🧟‍♂️
                    logger.LogError(jobEx, "Error crítico procesando el Job {JobId}.", workItem.JobId);

                    using var fallbackScope = scopeFactory.CreateScope();
                    var fallbackJobStore = fallbackScope.ServiceProvider.GetRequiredService<IJobStore>();

                    await fallbackJobStore.SetErrorAsync(workItem.JobId, $"Fallo crítico interno: {jobEx.Message}", "Failed");

                    if (!string.IsNullOrWhiteSpace(workItem.ConnectionId))
                    {
                        await hubContext.Clients.Client(workItem.ConnectionId)
                            .SendAsync("JobFailed", new { workItem.JobId, Error = $"Fallo crítico interno: {jobEx.Message}" }, ct);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // Si workItem es null aquí, fue un fallo antes del dequeue
                logger.LogError(ex, "Error fatal en el bucle de cola del InferenceWorker.");
            }
        }
    }
}