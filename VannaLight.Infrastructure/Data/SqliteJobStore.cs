using Dapper;
using Microsoft.Data.Sqlite;
using System;
using System.Threading;
using System.Threading.Tasks;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;
using VannaLight.Core.Settings;

namespace VannaLight.Infrastructure.Data;

public class SqliteJobStore : IJobStore
{
    private readonly string _connString;

    public SqliteJobStore(RuntimeDbOptions options)
    {
        _connString = $"Data Source={options.DbPath};";
    }

    public async Task<Guid> CreateJobAsync(string userId, string role, string question, CancellationToken ct = default)
    {
        var jobId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        using var conn = new SqliteConnection(_connString);
        await conn.OpenAsync(ct);

        const string sql = @"
            INSERT INTO QuestionJobs (JobId, UserId, Role, Question, Status, CreatedUtc, UpdatedUtc)
            VALUES (@JobId, @UserId, @Role, @Question, @Status, @CreatedUtc, @UpdatedUtc)";

        await conn.ExecuteAsync(sql, new
        {
            JobId = jobId.ToString(),
            UserId = userId,
            Role = role,
            Question = question,
            Status = "Pending",
            CreatedUtc = now.ToString("O"),
            UpdatedUtc = now.ToString("O")
        });

        return jobId;
    }

    public async Task UpdateStatusAsync(Guid jobId, string status, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connString);
        await conn.OpenAsync(ct);

        const string sql = "UPDATE QuestionJobs SET Status = @Status, UpdatedUtc = @UpdatedUtc WHERE JobId = @JobId";
        await conn.ExecuteAsync(sql, new { JobId = jobId.ToString(), Status = status, UpdatedUtc = DateTime.UtcNow.ToString("O") });
    }

    public async Task SetResultAsync(Guid jobId, string resultJson, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connString);
        await conn.OpenAsync(ct);

        const string sql = "UPDATE QuestionJobs SET Status = 'Completed', ResultJson = @ResultJson, UpdatedUtc = @UpdatedUtc WHERE JobId = @JobId";
        await conn.ExecuteAsync(sql, new { JobId = jobId.ToString(), ResultJson = resultJson, UpdatedUtc = DateTime.UtcNow.ToString("O") });
    }

    public async Task SetErrorAsync(Guid jobId, string errorText, string status = "Failed", CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connString);
        await conn.OpenAsync(ct);

        const string sql = "UPDATE QuestionJobs SET Status = @Status, ErrorText = @ErrorText, UpdatedUtc = @UpdatedUtc WHERE JobId = @JobId";
        await conn.ExecuteAsync(sql, new { JobId = jobId.ToString(), Status = status, ErrorText = errorText, UpdatedUtc = DateTime.UtcNow.ToString("O") });
    }

    public async Task UpdateJobAsync(Guid jobId, string status, string? sqlText, string? resultJson, string? errorText, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connString);
        await conn.OpenAsync(ct);

        const string sql = @"
            UPDATE QuestionJobs 
            SET Status = @Status, SqlText = @SqlText, ResultJson = @ResultJson, ErrorText = @ErrorText, UpdatedUtc = @UpdatedUtc
            WHERE JobId = @JobId";

        await conn.ExecuteAsync(sql, new
        {
            JobId = jobId.ToString(),
            Status = status,
            SqlText = sqlText,
            ResultJson = resultJson,
            ErrorText = errorText,
            UpdatedUtc = DateTime.UtcNow.ToString("O")
        });
    }

    // --- NUEVO: Implementación del método de lectura requerido por IJobStore ---
    public async Task<QuestionJob?> GetJobAsync(Guid jobId, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connString);
        await conn.OpenAsync(ct);

        const string sql = "SELECT * FROM QuestionJobs WHERE JobId = @JobId";
        var raw = await conn.QuerySingleOrDefaultAsync<dynamic>(sql, new { JobId = jobId.ToString() });

        if (raw == null) return null;

        return new QuestionJob
        {
            JobId = Guid.Parse(raw.JobId),
            UserId = raw.UserId,
            Role = raw.Role,
            Question = raw.Question,
            Status = raw.Status,
            SqlText = raw.SqlText,
            ErrorText = raw.ErrorText,
            ResultJson = raw.ResultJson,
            Attempt = (int)(raw.Attempt ?? 0),
            TrainingExampleSaved = Convert.ToInt32(raw.TrainingExampleSaved ?? 0) == 1,
            CreatedUtc = DateTime.Parse(raw.CreatedUtc),
            UpdatedUtc = DateTime.Parse(raw.UpdatedUtc)
        };
    }
}