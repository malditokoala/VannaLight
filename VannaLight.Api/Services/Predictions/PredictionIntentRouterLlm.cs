using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Api.Services.Predictions;

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
Eres un enrutador analítico de manufactura. Tu tarea es extraer la intención de predicción de la pregunta del usuario.

REGLAS ESTRICTAS DE TIEMPO (MUY IMPORTANTE):
1. El modelo actual de ML.NET **SOLO** soporta pronósticos de MESES CALENDARIO FUTUROS (ej. 'próximo mes', 'en 2 meses').
2. Si el usuario pide predicciones para 'hoy', 'ayer', 'esta semana', 'este mes', 'acumulado', 'en curso' o 'al día de hoy', DEBES marcar ""IsSupportedByCurrentModel"": false.
3. Si lo rechazas, explica el motivo en ""UnsupportedReason"" (Ej. 'El modelo solo predice meses futuros completos, no días ni acumulados en curso.').

Devuelve SOLO un JSON válido con esta estructura:
{{
  ""IsPredictionRequest"": true/false,
  ""EntityName"": ""Número de parte exacto o null"",
  ""Horizon"": (int) meses a predecir, 1 por defecto,
  ""UserTechnicalLevel"": ""Operativo"" o ""Gerencial"",
  ""IsSupportedByCurrentModel"": true/false,
  ""UnsupportedReason"": ""Motivo del rechazo o null""
}}
<|im_end|>
<|im_start|>user
{question}
<|im_end|>
<|im_start|>assistant
{{";

        var rawResponse = "{" + await _llm.CompleteAsync(prompt, ct);
        var cleanJson = ExtractValidJson(rawResponse);

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<PredictionIntent>(cleanJson, options)
                   ?? new PredictionIntent { IsPredictionRequest = false };
        }
        catch
        {
            return new PredictionIntent { IsPredictionRequest = false };
        }
    }

    private string ExtractValidJson(string text)
    {
        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');
        if (start == -1 || end == -1 || end < start) return "{}";
        return text.Substring(start, end - start + 1);
    }
}