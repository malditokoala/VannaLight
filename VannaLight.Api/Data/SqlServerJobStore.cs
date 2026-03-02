using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace VannaLight.Api.Data;

public class QuestionJob
{
    public Guid JobId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string Status { get; set; } = "Queued";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public string? SqlText { get; set; }
    public string? ErrorText { get; set; }
    public string? ResultJson { get; set; }
    public int Attempt { get; set; }
    public bool TrainingExampleSaved { get; set; }
}

public interface IJobStore
{
    Task CreateJobAsync(QuestionJob job);
    Task UpdateStatusAsync(Guid jobId, string status);
    Task SetResultAsync(Guid jobId, string sqlText);
    Task SetErrorAsync(Guid jobId, string error, string status);
    Task<QuestionJob?> GetJobAsync(Guid jobId);
    Task<IEnumerable<QuestionJob>> ListUserJobsAsync(string userId, int take);
    Task<IEnumerable<QuestionJob>> GetStaleJobsAsync(); // Para rehidratación tras reinicios
}

public class SqlServerJobStore(IConfiguration config) : IJobStore
{
    private readonly string _conn = config.GetConnectionString("OperationalDb")
        ?? throw new InvalidOperationException("Falta OperationalDb en appsettings.");

    private IDbConnection CreateConnection() => new SqlConnection(_conn);

    public async Task CreateJobAsync(QuestionJob job)
    {
        const string sql = @"
            INSERT INTO dbo.QuestionJobs (JobId, UserId, [Role], Question, [Status], CreatedUtc, UpdatedUtc)
            VALUES (@JobId, @UserId, @Role, @Question, @Status, @CreatedUtc, @UpdatedUtc)";
        using var db = CreateConnection();
        await db.ExecuteAsync(sql, job);
    }

    public async Task UpdateStatusAsync(Guid jobId, string status)
    {
        const string sql = "UPDATE dbo.QuestionJobs SET [Status] = @Status, UpdatedUtc = @UpdatedUtc WHERE JobId = @JobId";
        using var db = CreateConnection();
        await db.ExecuteAsync(sql, new { JobId = jobId, Status = status, UpdatedUtc = DateTime.UtcNow });
    }

    public async Task SetResultAsync(Guid jobId, string sqlText)
    {
        const string sql = "UPDATE dbo.QuestionJobs SET [Status] = 'Completed', SqlText = @SqlText, UpdatedUtc = @UpdatedUtc WHERE JobId = @JobId";
        using var db = CreateConnection();
        await db.ExecuteAsync(sql, new { JobId = jobId, SqlText = sqlText, UpdatedUtc = DateTime.UtcNow });
    }

    public async Task SetErrorAsync(Guid jobId, string error, string status)
    {
        const string sql = "UPDATE dbo.QuestionJobs SET [Status] = @Status, ErrorText = @Error, UpdatedUtc = @UpdatedUtc WHERE JobId = @JobId";
        using var db = CreateConnection();
        await db.ExecuteAsync(sql, new { JobId = jobId, Status = status, Error = error, UpdatedUtc = DateTime.UtcNow });
    }

    public async Task<QuestionJob?> GetJobAsync(Guid jobId)
    {
        const string sql = "SELECT * FROM dbo.QuestionJobs WHERE JobId = @JobId";
        using var db = CreateConnection();
        return await db.QuerySingleOrDefaultAsync<QuestionJob>(sql, new { JobId = jobId });
    }

    public async Task<IEnumerable<QuestionJob>> ListUserJobsAsync(string userId, int take)
    {
        const string sql = "SELECT TOP (@Take) * FROM dbo.QuestionJobs WHERE UserId = @UserId ORDER BY CreatedUtc DESC";
        using var db = CreateConnection();
        return await db.QueryAsync<QuestionJob>(sql, new { UserId = userId, Take = take });
    }

    public async Task<IEnumerable<QuestionJob>> GetStaleJobsAsync()
    {
        const string sql = "SELECT * FROM dbo.QuestionJobs WHERE [Status] IN ('Queued', 'Analyzing', 'Validating')";
        using var db = CreateConnection();
        return await db.QueryAsync<QuestionJob>(sql);
    }
}