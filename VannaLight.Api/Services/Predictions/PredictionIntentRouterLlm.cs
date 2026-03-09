using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VannaLight.Core.Models; // CAMBIO: Usar modelos del Core
using VannaLight.Core.Abstractions; // NUEVO: Importar la interfaz del Core
using VannaLight.Infrastructure.AI;

namespace VannaLight.Api.Services.Predictions;

// BORRAMOS LA INTERFAZ QUE ESTABA AQUÍ ARRIBA

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
Eres un enrutador analítico para un sistema industrial. Analiza si la pregunta del usuario requiere pronosticar o predecir datos futuros (ej. ventas futuras, demanda, producción del próximo mes).
Devuelve SOLO un objeto JSON válido.

Reglas del JSON:
1. 'IsPredictionRequest': true si pregunta por el futuro o pronósticos. false si pregunta por el pasado o presente.
2. 'EntityName': El nombre del producto, número de parte o familia a predecir. Null si no especifica.
3. 'Horizon': Número entero de meses a predecir (1 por defecto si no especifica).
4. 'UserTechnicalLevel': 'Operativo' si la pregunta es directa y técnica, 'Gerencial' si usa palabras como 'tendencia', 'estrategia' o 'proyección'.

Pregunta del usuario: {question}
Respuesta JSON:";

        var rawResponse = await _llm.CompleteAsync(prompt, ct);
        var cleanJson = ExtractValidJson(rawResponse);

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var intent = JsonSerializer.Deserialize<PredictionIntent>(cleanJson, options);
            return intent ?? new PredictionIntent { IsPredictionRequest = false };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ML ROUTER ERROR] Fallo al parsear JSON. Error: {ex.Message}. JSON: {cleanJson}");
            return new PredictionIntent { IsPredictionRequest = false };
        }
    }

    // Algoritmo de balanceo de llaves (El mismo blindaje que usamos en Docs)
    private string ExtractValidJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "{}";
        int startIndex = text.IndexOf('{');
        if (startIndex == -1) return "{}";

        int braceCount = 0;
        for (int i = startIndex; i < text.Length; i++)
        {
            if (text[i] == '{') braceCount++;
            else if (text[i] == '}') braceCount--;

            if (braceCount == 0) return text.Substring(startIndex, i - startIndex + 1);
        }
        return "{}";
    }
}