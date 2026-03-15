using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Api.Services.Predictions;

/// <summary>
/// Enrutador semántico que discrimina entre consultas SQL y Predicciones de ML.
/// </summary>
public class PredictionIntentRouterLlm : IPredictionIntentRouter
{
    private readonly ILlmClient _llm;

    public PredictionIntentRouterLlm(ILlmClient llm)
    {
        _llm = llm;
    }

    public async Task<PredictionIntent> ParseAsync(string question, CancellationToken ct)
    {
        var prompt = $@"
<|im_start|>system
Eres el enrutador analítico de VannaLight.
Tu objetivo es discriminar entre consultas de DATOS REALES y PRONÓSTICOS FUTUROS (ML).

REGLA DE ORO:
- DATOS EN CURSO O PASADOS -> IsPredictionRequest = false. Ejemplos: ""hoy"", ""ayer"", ""este turno"", ""cuánto lleva"", ""acumulado"", ""actual"".
- PRONÓSTICOS FUTUROS -> IsPredictionRequest = true. Ejemplos: ""pronóstico cierre de turno"", ""próximo turno"", ""mañana"", ""próximo mes"".

Si la pregunta es de pronóstico futuro para un Número de Parte, debes mapearla a uno de estos ""PredictionTarget"":
- ""EndOfCurrentShift"" (ej. cierre de turno actual, final de turno)
- ""NextShift"" (ej. próximo turno)
- ""Tomorrow"" (ej. pronóstico de mañana)
- ""NextMonth"" (ej. próximo mes completo)

Si el usuario pide predecir periodos ambiguos o en curso, marca IsPredictionRequest = false y pon en UnsupportedReason: ""Datos en curso o periodo ambiguo. Se procesará vía SQL normal.""

Retorna SOLO un JSON válido:
{{
  ""IsPredictionRequest"": true/false,
  ""EntityName"": ""Número de Parte o null"",
  ""PredictionTarget"": ""EndOfCurrentShift, NextShift, Tomorrow o NextMonth"",
  ""UnsupportedReason"": ""Motivo de rechazo o null""
}}
<|im_end|>
<|im_start|>user
{question}
<|im_end|>
<|im_start|>assistant
{{";

        // Ejecución de la inferencia
        var rawResponse = "{" + await _llm.CompleteAsync(prompt, ct);
        var cleanJson = ExtractValidJson(rawResponse);

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<PredictionIntent>(cleanJson, options)
                   ?? new PredictionIntent { IsPredictionRequest = false };
        }
        catch (Exception)
        {
            // En caso de fallo crítico del LLM, devolvemos un objeto de intención fallida seguro
            return new PredictionIntent
            {
                IsPredictionRequest = false,
                UnsupportedReason = "Error al interpretar la intención de la pregunta."
            };
        }
    }

    /// <summary>
    /// Utiliza Regex para garantizar la extracción de un JSON válido incluso si el LLM agrega ruido.
    /// </summary>
    private string ExtractValidJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "{}";
        var match = Regex.Match(text, @"\{[\s\S]*\}");
        return match.Success ? match.Value : "{}";
    }
}