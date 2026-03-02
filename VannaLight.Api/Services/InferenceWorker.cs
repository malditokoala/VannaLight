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

        // 1. Obtenemos las rutas y la conexión a la base de datos (Northwind para el Dry-Run)
        string sqlitePath = configuration["Paths:Sqlite"] ?? "vanna_memory.db";
        string sqlServerConnString = configuration.GetConnectionString("OperationalDb")
            ?? throw new InvalidOperationException("Falta la cadena de conexión en el appsettings.");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var workItem = await queue.DequeueAsync(ct);

                using var scope = scopeFactory.CreateScope();
                var jobStore = scope.ServiceProvider.GetRequiredService<IJobStore>();
                var askUseCase = scope.ServiceProvider.GetRequiredService<AskUseCase>();

                await jobStore.UpdateStatusAsync(workItem.JobId, "Analyzing");
                await hubContext.Clients.Client(workItem.ConnectionId)
                    .SendAsync("JobStatusUpdated", new { workItem.JobId, Status = "Analyzing" }, ct);

                // 2. Llamamos a tu UseCase con los 4 parámetros exactos que pide tu código
                var result = await askUseCase.ExecuteAsync(
                    workItem.Question,
                    sqlitePath,
                    sqlServerConnString,
                    ct);

                // 3. Evaluamos usando tus propiedades: Success, Sql, Error
                // ... tu código anterior (var result = await askUseCase...)

                if (result.Success)
                {
                    // 1. EL NUEVO PASO: ¡Ejecutar el SQL de verdad!
                    IEnumerable<dynamic> queryResults = Array.Empty<dynamic>();
                    string? executionError = null;

                    try
                    {
                        using var connection = new SqlConnection(sqlServerConnString);
                        // Dapper ejecuta la consulta y mapea las columnas mágicamente
                        queryResults = await connection.QueryAsync(result.Sql);
                    }
                    catch (Exception ex)
                    {
                        executionError = ex.Message;
                    }

                    if (executionError == null)
                    {
                        // Se ejecutó perfecto, mandamos SQL y los DATOS
                        await jobStore.SetResultAsync(workItem.JobId, result.Sql);
                        await hubContext.Clients.Client(workItem.ConnectionId)
                            .SendAsync("JobCompleted", new
                            {
                                workItem.JobId,
                                Sql = result.Sql,
                                Data = queryResults // <--- Aquí va el JSON con tus filas de Northwind
                            }, ct);
                    }
                    else
                    {
                        // Compiló en el Dry-Run pero falló al ejecutar (ej. timeout o error de datos)
                        await jobStore.SetErrorAsync(workItem.JobId, $"Error de ejecución: {executionError}", "Failed");
                        await hubContext.Clients.Client(workItem.ConnectionId)
                            .SendAsync("JobFailed", new { workItem.JobId, Error = $"El SQL es válido pero falló al ejecutarse: {executionError}" }, ct);
                    }
                }
                else
                {
                    // ... el else original que ya tenías ...
                    await jobStore.SetErrorAsync(workItem.JobId, result.Error ?? "Error desconocido", "Failed");
                    await hubContext.Clients.Client(workItem.ConnectionId)
                        .SendAsync("JobFailed", new { workItem.JobId, Error = result.Error }, ct);
                }
               
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error crítico procesando inferencia.");
            }
        }
    }
}