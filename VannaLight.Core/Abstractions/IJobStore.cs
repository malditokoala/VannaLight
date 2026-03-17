using System;
using System.Threading;
using System.Threading.Tasks;
using VannaLight.Core.Models;

namespace VannaLight.Core.Abstractions;

public interface IJobStore
{
    // Se añade el parámetro 'mode' al crear el Job
    Task<Guid> CreateJobAsync(string userId, string role, string question, string mode = "Data", CancellationToken ct = default);
    Task UpdateStatusAsync(Guid jobId, string status, CancellationToken ct = default);
    Task SetResultAsync(Guid jobId, string resultJson, CancellationToken ct = default);
    Task SetErrorAsync(Guid jobId, string errorText, string status = "Failed", CancellationToken ct = default);
    Task UpdateJobAsync(Guid jobId, string status, string? sqlText, string? resultJson, string? errorText, CancellationToken ct = default);
    Task<QuestionJob?> GetJobAsync(Guid jobId, CancellationToken ct = default);

    // Se añade el filtro 'mode' para la consulta de historial
    Task<IEnumerable<QuestionJob>> GetRecentJobsAsync(int limit = 20, string? mode = null, CancellationToken ct = default);
    Task<bool> UpdateFeedbackAsync(Guid jobId, string verificationStatus, string? comment = null, CancellationToken ct = default);
    Task<bool> MarkTrainingExampleSavedAsync(
    Guid jobId,
    string correctedSql,
    string verificationStatus,
    string? comment = null,
    CancellationToken ct = default);
}