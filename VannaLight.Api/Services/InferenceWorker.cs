using Dapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
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
            try
            {
                // 1. Sacamos el trabajo de la cola
                var workItem = await queue.DequeueAsync(ct);

                // 2. AISLAMOS EL TRABAJO: Si este Job específico falla, lo cachamos aquí adentro.
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var jobStore = scope.ServiceProvider.GetRequiredService<IJobStore>();
                    var askUseCase = scope.ServiceProvider.GetRequiredService<AskUseCase>();

                    await jobStore.UpdateStatusAsync(workItem.JobId, "Analyzing");
                    await hubContext.Clients.Client(workItem.ConnectionId)
                        .SendAsync("JobStatusUpdated", new { workItem.JobId, Status = "Analyzing" }, ct);

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
                            await hubContext.Clients.Client(workItem.ConnectionId)
                                .SendAsync("JobCompleted", new
                                {
                                    workItem.JobId,
                                    Sql = result.Sql,
                                    Data = queryResults
                                }, ct);
                        }
                        else
                        {
                            await jobStore.SetErrorAsync(workItem.JobId, $"Error de ejecución: {executionError}", "Failed");
                            await hubContext.Clients.Client(workItem.ConnectionId)
                                .SendAsync("JobFailed", new { workItem.JobId, Error = $"El SQL es válido pero falló al ejecutarse: {executionError}" }, ct);
                        }
                    }
                    else
                    {
                        await jobStore.SetErrorAsync(workItem.JobId, result.Error ?? "Error desconocido", "Failed");
                        await hubContext.Clients.Client(workItem.ConnectionId)
                            .SendAsync("JobFailed", new { workItem.JobId, Error = result.Error }, ct);
                    }
                }
                catch (Exception jobEx)
                {
                    // 3. AQUÍ MATAMOS AL ZOMBIE 🧟‍♂️
                    logger.LogError(jobEx, "Error crítico procesando el Job {JobId}.", workItem.JobId);

                    // Creamos un nuevo Scope de emergencia por si el anterior se corrompió con el error
                    using var fallbackScope = scopeFactory.CreateScope();
                    var fallbackJobStore = fallbackScope.ServiceProvider.GetRequiredService<IJobStore>();

                    // Actualizamos la base de datos para no dejar el estado pegado en 'Analyzing'
                    await fallbackJobStore.SetErrorAsync(workItem.JobId, $"Fallo crítico interno: {jobEx.Message}", "Failed");

                    // Le enviamos la señal al frontend para quitar el loader y mostrar el recuadro rojo
                    await hubContext.Clients.Client(workItem.ConnectionId)
                        .SendAsync("JobFailed", new { workItem.JobId, Error = $"Fallo crítico interno: {jobEx.Message}" }, ct);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fatal en el bucle de cola del InferenceWorker.");
            }
        }
    }
}