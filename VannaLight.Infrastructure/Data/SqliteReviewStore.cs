using Dapper;
using Microsoft.Data.Sqlite;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Infrastructure.Data;

public class SqliteReviewStore : IReviewStore
{
    public async Task InitializeAsync(string sqlitePath, CancellationToken ct)
    {
        using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        await connection.OpenAsync(ct);

        var sql = @"
            CREATE TABLE IF NOT EXISTS ReviewQueue (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Question TEXT NOT NULL,
                GeneratedSql TEXT NOT NULL,
                ErrorMessage TEXT,
                Status TEXT NOT NULL,
                Reason TEXT NOT NULL,
                CreatedUtc DATETIME NOT NULL
            );";
        await connection.ExecuteAsync(sql);
    }

    public async Task<long> EnqueueAsync(string sqlitePath, string question, string generatedSql, string? errorMessage, string reason, CancellationToken ct)
    {
        using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        var query = @"
            INSERT INTO ReviewQueue (Question, GeneratedSql, ErrorMessage, Status, Reason, CreatedUtc) 
            VALUES (@Question, @GeneratedSql, @ErrorMessage, 'Pending', @Reason, @Now)
            RETURNING Id;";

        return await connection.ExecuteScalarAsync<long>(query, new
        {
            Question = question,
            GeneratedSql = generatedSql,
            ErrorMessage = errorMessage,
            Reason = reason,
            Now = DateTime.UtcNow
        });
    }

    public async Task<IReadOnlyList<ReviewItem>> GetPendingReviewsAsync(string sqlitePath, CancellationToken ct)
    {
        using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        var result = await connection.QueryAsync<ReviewItem>(
            "SELECT * FROM ReviewQueue WHERE Status = 'Pending' ORDER BY CreatedUtc ASC");
        return result.ToList();
    }

    public async Task<ReviewItem?> GetReviewByIdAsync(string sqlitePath, long id, CancellationToken ct)
    {
        using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        return await connection.QuerySingleOrDefaultAsync<ReviewItem>(
            "SELECT * FROM ReviewQueue WHERE Id = @Id", new { Id = id });
    }

    public async Task UpdateReviewStatusAsync(string sqlitePath, long id, string status, CancellationToken ct)
    {
        using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        await connection.ExecuteAsync(
            "UPDATE ReviewQueue SET Status = @Status WHERE Id = @Id",
            new { Status = status, Id = id });
    }
}