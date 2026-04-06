namespace VannaLight.Core.Models;

/// <summary>
/// Representa la intención de predicción detectada por el LLM y procesada por ML.NET.
/// </summary>
public class PredictionIntent
{
    // --- 1. Datos del Router (LLM) ---
    public bool IsPredictionRequest { get; set; }

    /// <summary>
    /// Identificador de la serie a proyectar. Puede ser un número de parte, producto, categoría,
    /// país, cliente u otra clave de serie según el dominio.
    /// </summary>
    public string? EntityName { get; set; }

    /// <summary>
    /// Métrica objetivo inferida para el forecast. Ejemplos: scrap_qty, units_sold,
    /// net_sales, order_count, produced_qty, downtime_minutes.
    /// </summary>
    public string? MetricKey { get; set; }

    /// <summary>
    /// Tipo semántico de la serie. Ejemplos: part, product, category, customer,
    /// ship_country, employee, press, department.
    /// </summary>
    public string? SeriesType { get; set; }

    /// <summary>
    /// Objetivo temporal normalizado del forecast.
    /// Valores soportados actualmente:
    /// EndOfCurrentShift = cierre del bucket actual,
    /// NextShift = siguiente bucket disponible,
    /// Tomorrow = siguiente día completo,
    /// NextMonth = siguiente mes completo.
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
    /// Cantidad de buckets históricos utilizados para alimentar el autoregresor.
    /// </summary>
    public int HistoryShiftsUsed { get; set; }

    // --- 4. Mensaje Final (Humanizador) ---
    public string? HumanizedMessage { get; set; }
}
