using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VannaLight.Api.Contracts;
using VannaLight.Api.Hubs;
using VannaLight.Core.Abstractions;
using VannaLight.Core.UseCases;

namespace VannaLight.Api.Services;

public class InferenceWorker : BackgroundService
{
    private readonly IAskRequestQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InferenceWorker> _logger;
    private readonly IHubContext<AssistantHub> _hubContext;

    public InferenceWorker(
        IAskRequestQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<InferenceWorker> logger,
        IHubContext<AssistantHub> hubContext)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("InferenceWorker iniciado y esperando preguntas...");

        while (!stoppingToken.IsCancellationRequested)
        {
            var workItem = await _queue.DequeueAsync(stoppingToken);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var jobStore = scope.ServiceProvider.GetRequiredService<IJobStore>();

                await jobStore.UpdateStatusAsync(workItem.JobId, "Processing");

                if (!string.IsNullOrWhiteSpace(workItem.ConnectionId))
                {
                    await _hubContext.Clients.Client(workItem.ConnectionId)
                        .SendAsync("JobStatusUpdated", new { workItem.JobId, Status = "Analyzing" }, stoppingToken);
                }

                // ==========================================
                // 1) MODO DOCUMENTOS (PDF)
                // ==========================================
                if (workItem.Mode == AskMode.Docs)
                {
                    var docsService = scope.ServiceProvider.GetRequiredService<IDocsAnswerService>();
                    var docsResult = await docsService.AnswerAsync(workItem.Question, stoppingToken);

                    if (docsResult.Success)
                    {
                        var jsonPayload = JsonSerializer.Serialize(new { type = "docs", answer = docsResult.AnswerText, confidence = 0.95 });
                        await jobStore.SetResultAsync(workItem.JobId, jsonPayload);

                        if (!string.IsNullOrWhiteSpace(workItem.ConnectionId))
                        {
                            await _hubContext.Clients.Client(workItem.ConnectionId)
                                .SendAsync("JobCompleted", new { workItem.JobId, Mode = "Docs", ResultJson = jsonPayload }, stoppingToken);
                        }
                    }
                    else
                    {
                        await jobStore.SetErrorAsync(workItem.JobId, docsResult.ErrorMessage ?? "Error en documentos.", "Failed");
                        if (!string.IsNullOrWhiteSpace(workItem.ConnectionId))
                            await _hubContext.Clients.Client(workItem.ConnectionId)
                                .SendAsync("JobFailed", new { workItem.JobId, Error = docsResult.ErrorMessage }, stoppingToken);
                    }
                    continue;
                }

                // ==========================================
                // 2) MODO PREDICCIONES (ML.NET)
                // ==========================================
                if (workItem.Mode == AskMode.Predict)
                {
                    var askUseCase = scope.ServiceProvider.GetRequiredService<AskUseCase>();

                    // Llamamos al nuevo método exclusivo de predicciones
                    var predictResult = await askUseCase.PredictAsync(workItem.Question, stoppingToken);

                    if (predictResult.Success)
                    {
                        // Guardamos el JSON y lo mandamos a la UI
                        await jobStore.SetResultAsync(workItem.JobId, predictResult.ResultJson ?? "");

                        if (!string.IsNullOrWhiteSpace(workItem.ConnectionId))
                        {
                            await _hubContext.Clients.Client(workItem.ConnectionId)
                                .SendAsync("JobCompleted", new
                                {
                                    JobId = workItem.JobId,
                                    Mode = "Predict",
                                    ResultJson = predictResult.ResultJson
                                }, stoppingToken);
                        }
                    }
                    else
                    {
                        await jobStore.SetErrorAsync(workItem.JobId, predictResult.Error ?? "Error en la predicción.", "Failed");

                        if (!string.IsNullOrWhiteSpace(workItem.ConnectionId))
                        {
                            await _hubContext.Clients.Client(workItem.ConnectionId)
                                .SendAsync("JobFailed", new { JobId = workItem.JobId, Error = predictResult.Error }, stoppingToken);
                        }
                    }

                    continue; // Evita que pase al modo SQL
                }

                // ==========================================
                // 3) MODO DATOS (SQL)
                // ==========================================
                if (workItem.Mode == AskMode.Data)
                {
                    var askUseCase = scope.ServiceProvider.GetRequiredService<AskUseCase>();
                    var config = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();

                    var sqlitePath = config["Paths:Sqlite"] ?? "Data/vanna_memory.db";
                    var sqlConn = config.GetConnectionString("OperationalDb");

                    var result = await askUseCase.ExecuteAsync(workItem.Question, sqlitePath, sqlConn, stoppingToken);

                    if (result.Success)
                    {
                        object? queryData = null;

                        if (result.PassedDryRun)
                        {
                            using var conn = new Microsoft.Data.SqlClient.SqlConnection(sqlConn);
                            queryData = await Dapper.SqlMapper.QueryAsync(conn, result.Sql);
                        }

                        var jsonPayload = JsonSerializer.Serialize(new
                        {
                            type = "data",
                            sql = result.Sql,
                            data = queryData
                        });

                        await jobStore.UpdateJobAsync(
                            workItem.JobId,
                            "Completed",
                            result.Sql,
                            jsonPayload,
                            null,
                            stoppingToken
                        );

                        if (!string.IsNullOrWhiteSpace(workItem.ConnectionId))
                        {
                            await _hubContext.Clients.Client(workItem.ConnectionId)
                                .SendAsync("JobCompleted", new
                                {
                                    workItem.JobId,
                                    Mode = "Data",
                                    Sql = result.Sql,
                                    Data = queryData
                                }, stoppingToken);
                        }
                    }
                    else
                    {
                        await jobStore.SetErrorAsync(workItem.JobId, result.Error ?? "Error SQL", "Failed");

                        if (!string.IsNullOrWhiteSpace(workItem.ConnectionId))
                        {
                            await _hubContext.Clients.Client(workItem.ConnectionId)
                                .SendAsync("JobFailed", new { workItem.JobId, Error = result.Error }, stoppingToken);
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando JobId {JobId}", workItem.JobId);

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var jobStore = scope.ServiceProvider.GetRequiredService<IJobStore>();
                    await jobStore.SetErrorAsync(workItem.JobId, ex.Message, "Failed");

                    if (!string.IsNullOrWhiteSpace(workItem.ConnectionId))
                    {
                        await _hubContext.Clients.Client(workItem.ConnectionId)
                            .SendAsync("JobFailed", new { workItem.JobId, Error = "Fallo crítico interno: " + ex.Message }, stoppingToken);
                    }
                }
                catch { /* ignorar errores en el manejo de errores */ }
            }
        }
    }
}