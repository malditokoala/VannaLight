using Dapper;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
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
        _connString = $"Data Source={options.DbPath}";
        EnsureColumnsExist();
    }

    private void EnsureColumnsExist()
    {
        using var conn = new SqliteConnection(_connString);
        conn.Open();

        TryAddColumn(conn, "ALTER TABLE QuestionJobs ADD COLUMN VerificationStatus TEXT DEFAULT 'Pending';");
        TryAddColumn(conn, "ALTER TABLE QuestionJobs ADD COLUMN FeedbackComment TEXT;");
        TryAddColumn(conn, "ALTER TABLE QuestionJobs ADD COLUMN Mode TEXT DEFAULT 'Data';");
    }

    private static void TryAddColumn(SqliteConnection conn, string sql)
    {
        try
        {
            conn.Execute(sql);
        }
        catch (SqliteException ex) when (ex.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase))
        {
            // OK: la columna ya existe
        }
    }

    public async Task<Guid> CreateJobAsync(
        string userId,
        string role,
        string question,
        string mode = "Data",
        CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connString);
        var jobId = Guid.NewGuid();

        var command = new CommandDefinition(
            @"INSERT INTO QuestionJobs
              (JobId, UserId, Role, Question, Status, Mode, CreatedUtc, UpdatedUtc)
              VALUES
              (@Id, @UserId, @Role, @Question, 'Queued', @Mode, DATETIME('now'), DATETIME('now'))",
            new
            {
                Id = jobId.ToString(),
                UserId = userId,
                Role = role,
                Question = question,
                Mode = mode
            },
            cancellationToken: ct);

        await conn.ExecuteAsync(command);
        return jobId;
    }

    public async Task UpdateStatusAsync(Guid jobId, string status, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connString);

        var command = new CommandDefinition(
            "UPDATE QuestionJobs SET Status = @Status, UpdatedUtc = DATETIME('now') WHERE JobId = @Id",
            new
            {
                Status = status,
                Id = jobId.ToString()
            },
            cancellationToken: ct);

        await conn.ExecuteAsync(command);
    }

    public async Task SetResultAsync(Guid jobId, string resultJson, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connString);

        var command = new CommandDefinition(
            "UPDATE QuestionJobs SET Status = 'Completed', ResultJson = @Result, UpdatedUtc = DATETIME('now') WHERE JobId = @Id",
            new
            {
                Result = resultJson,
                Id = jobId.ToString()
            },
            cancellationToken: ct);

        await conn.ExecuteAsync(command);
    }

    public async Task SetErrorAsync(Guid jobId, string errorText, string status = "Failed", CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connString);

        var command = new CommandDefinition(
            "UPDATE QuestionJobs SET Status = @Status, ErrorText = @Error, UpdatedUtc = DATETIME('now') WHERE JobId = @Id",
            new
            {
                Status = status,
                Error = errorText,
                Id = jobId.ToString()
            },
            cancellationToken: ct);

        await conn.ExecuteAsync(command);
    }

    public async Task UpdateJobAsync(
        Guid jobId,
        string status,
        string? sqlText,
        string? resultJson,
        string? errorText,
        CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connString);

        var command = new CommandDefinition(
            @"UPDATE QuestionJobs
              SET Status = @Status,
                  SqlText = @Sql,
                  ResultJson = @Json,
                  ErrorText = @Error,
                  UpdatedUtc = DATETIME('now')
              WHERE JobId = @Id",
            new
            {
                Status = status,
                Sql = sqlText,
                Json = resultJson,
                Error = errorText,
                Id = jobId.ToString()
            },
            cancellationToken: ct);

        await conn.ExecuteAsync(command);
    }

    public async Task<QuestionJob?> GetJobAsync(Guid jobId, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connString);

        var command = new CommandDefinition(
            """
                SELECT 
                    JobId, UserId, Role, Question, Status, Mode, SqlText, ErrorText, ResultJson,
                    Attempt, TrainingExampleSaved, VerificationStatus, FeedbackComment,
                    CreatedUtc, UpdatedUtc
                FROM QuestionJobs 
                WHERE JobId = @Id
            """,
            new { Id = jobId.ToString() },
            cancellationToken: ct);

        var raw = await conn.QuerySingleOrDefaultAsync<QuestionJobRow>(command);
        return raw == null ? null : MapToJob(raw);
    }

    public async Task<IEnumerable<QuestionJob>> GetRecentJobsAsync(
    int limit = 20,
    string? mode = null,
    CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connString);
        await conn.OpenAsync(ct);

        var command = new CommandDefinition(
            """
                SELECT 
                    JobId, UserId, Role, Question, Status, Mode, SqlText, ErrorText, ResultJson,
                    Attempt, TrainingExampleSaved, VerificationStatus, FeedbackComment,
                    CreatedUtc, UpdatedUtc
                FROM QuestionJobs
                WHERE (@Mode IS NULL OR Mode = @Mode)
                ORDER BY CreatedUtc DESC
                LIMIT @Limit
             """,
            new
            {
                Limit = limit,
                Mode = mode
            },
            cancellationToken: ct);

        var rawJobs = await conn.QueryAsync<QuestionJobRow>(command);

        var result = new List<QuestionJob>();
        foreach (var raw in rawJobs)
            result.Add(MapToJob(raw));

        return result;
    }

    public async Task<bool> UpdateFeedbackAsync(Guid jobId, string verificationStatus, string? comment = null, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connString);

        var command = new CommandDefinition(
            @"UPDATE QuestionJobs
              SET VerificationStatus = @Status,
                  FeedbackComment = @Comment,
                  UpdatedUtc = DATETIME('now')
              WHERE JobId = @Id",
            new
            {
                Id = jobId.ToString(),
                Status = verificationStatus,
                Comment = comment
            },
            cancellationToken: ct);

        var rows = await conn.ExecuteAsync(command);
        return rows > 0;
    }

    private static QuestionJob MapToJob(QuestionJobRow raw)
    {
        return new QuestionJob
        {
            JobId = Guid.TryParse(raw.JobId, out var parsedId) ? parsedId : Guid.Empty,
            UserId = raw.UserId ?? string.Empty,
            Role = raw.Role ?? string.Empty,
            Question = raw.Question ?? string.Empty,
            Status = raw.Status ?? "Unknown",
            Mode = raw.Mode ?? "Data",
            SqlText = raw.SqlText,
            ErrorText = raw.ErrorText,
            ResultJson = raw.ResultJson,
            Attempt = raw.Attempt,
            TrainingExampleSaved = raw.TrainingExampleSaved,
            VerificationStatus = raw.VerificationStatus ?? "Pending",
            FeedbackComment = raw.FeedbackComment,
            CreatedUtc = DateTime.TryParse(raw.CreatedUtc, out var created) ? created : DateTime.MinValue,
            UpdatedUtc = DateTime.TryParse(raw.UpdatedUtc, out var updated) ? updated : DateTime.MinValue
        };
    }

    public async Task<bool> UpdateJobReviewAsync(
    Guid jobId,
    string correctedSql,
    string verificationStatus,
    string? comment = null,
    CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connString);

        var command = new CommandDefinition(
            @"UPDATE QuestionJobs
          SET SqlText = @SqlText,
              TrainingExampleSaved = 1,
              VerificationStatus = @VerificationStatus,
              FeedbackComment = @Comment,
              UpdatedUtc = DATETIME('now')
          WHERE JobId = @Id",
            new
            {
                Id = jobId.ToString(),
                SqlText = correctedSql,
                VerificationStatus = verificationStatus,
                Comment = comment
            },
            cancellationToken: ct);

        var rows = await conn.ExecuteAsync(command);
        return rows > 0;
    }
    private sealed class QuestionJobRow
    {
        public string JobId { get; set; } = string.Empty;
        public string? UserId { get; set; }
        public string? Role { get; set; }
        public string? Question { get; set; }
        public string? Status { get; set; }
        public string? Mode { get; set; }
        public string? SqlText { get; set; }
        public string? ErrorText { get; set; }
        public string? ResultJson { get; set; }
        public int Attempt { get; set; }
        public int TrainingExampleSaved { get; set; }
        public string? VerificationStatus { get; set; }
        public string? FeedbackComment { get; set; }
        public string CreatedUtc { get; set; } = string.Empty;
        public string? UpdatedUtc { get; set; }
    }
}
