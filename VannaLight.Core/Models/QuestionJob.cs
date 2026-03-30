using System;
using System.Collections.Generic;
using System.Text;

namespace VannaLight.Core.Models;

public class QuestionJob
{
    public Guid JobId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string TenantKey { get; set; } = "default";
    public string Domain { get; set; } = string.Empty;
    public string ConnectionName { get; set; } = "OperationalDb";
    public string Question { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    // --- NUEVO: Etiqueta para separar SQL (Data) de ML (Predict) ---
    public string Mode { get; set; } = "Data";

    public string? SqlText { get; set; }
    public string? ErrorText { get; set; }
    public string? ResultJson { get; set; }
    public int Attempt { get; set; }
    public int TrainingExampleSaved { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    public string VerificationStatus { get; set; } = "Pending";
    public string? UserFeedback { get; set; }
    public DateTime? FeedbackUtc { get; set; }
    public string? FeedbackComment { get; set; }
}
