namespace VannaLight.Core.Models;

/// <summary>
/// Representa la intención de predicción detectada por el LLM y procesada por ML.NET.
/// </summary>
public class PredictionIntent
{
    // --- 1. Datos del Router (LLM) ---
    public bool IsPredictionRequest { get; set; }
    public string? EntityName { get; set; }

    /// <summary>
    /// Objetivo de la predicción (EndOfCurrentShift, NextShift, Tomorrow, NextMonth).
    /// </summary>
    public string? PredictionTarget { get; set; }

    // --- 2. Soporte y Validación ---
    public bool IsSupportedByCurrentModel { get; set; } = true;
    public string? UnsupportedReason { get; set; }

    // --- 3. Resultados de ML.NET (ForecastingService) ---
    public bool IsSuccess { get; set; }
    public float? PredictedValue { get; set; }

    /// <summary>
    /// Etiqueta legible del periodo (Ej: "Cierre de Turno 1 (12/Mar)").
    /// </summary>
    public string? ForecastPeriodLabel { get; set; }

    /// <summary>
    /// Cantidad de turnos históricos utilizados para alimentar el autoregresor.
    /// </summary>
    public int HistoryShiftsUsed { get; set; }

    // --- 4. Mensaje Final (Humanizador) ---
    public string? HumanizedMessage { get; set; }
}