using System;
using System.Collections.Generic;
using System.Text;

namespace VannaLight.Core.Models;

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