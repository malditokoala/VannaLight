using System;
using System.Collections.Generic;
using System.Text;

namespace VannaLight.Core.Models;

public class PredictionIntent
{
    // Datos extraídos por el LLM
    public bool IsPredictionRequest { get; set; }
    public string? EntityName { get; set; }
    public int Horizon { get; set; } = 1;
    public string? UserTechnicalLevel { get; set; }

    // Resultados llenados por ML.NET
    public bool IsSuccess { get; set; }
    public float? PredictedValue { get; set; }
    public double? ConfidenceScore { get; set; }

    // Resultado final llenado por el LLM humanizador
    public string? HumanizedMessage { get; set; }
}
