namespace VannaLight.Core.Models;

public class PredictionIntent
{
    // --- PASO A: Detección Básica ---
    public bool IsPredictionRequest { get; set; }
    public string? EntityName { get; set; }
    public int Horizon { get; set; } = 1;
    public string? UserTechnicalLevel { get; set; }

    // --- PASO B: Barrera de Soporte (NUEVO) ---
    public bool IsSupportedByCurrentModel { get; set; } = true;
    public string? UnsupportedReason { get; set; }

    // --- PASO C: Resultados del ML.NET (ACTUALIZADO) ---
    public bool IsSuccess { get; set; }
    public float? PredictedValue { get; set; }

    // Eliminamos el falso 'ConfidenceScore'
    // Agregamos metadatos reales para transparencia
    public string? ForecastPeriodLabel { get; set; }
    public int HistoryMonthsUsed { get; set; }

    // --- PASO D: Mensaje Final ---
    public string? HumanizedMessage { get; set; }
}