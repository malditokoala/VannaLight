using System;
using System.Threading;
using System.Threading.Tasks;
using VannaLight.Core.Models;

namespace VannaLight.Core.Abstractions;

public interface IJobStore
{
    // Métodos de Escritura (Commands)
    Task<Guid> CreateJobAsync(string userId, string role, string question, CancellationToken ct = default);

    Task UpdateStatusAsync(Guid jobId, string status, CancellationToken ct = default);

    Task SetResultAsync(Guid jobId, string resultJson, CancellationToken ct = default);

    Task SetErrorAsync(Guid jobId, string errorText, string status = "Failed", CancellationToken ct = default);

    Task UpdateJobAsync(Guid jobId, string status, string? sqlText, string? resultJson, string? errorText, CancellationToken ct = default);

    // Método de Lectura (Queries) necesario para el Controlador
    Task<QuestionJob?> GetJobAsync(Guid jobId, CancellationToken ct = default);
}