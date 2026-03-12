using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Api.Services.Predictions;

public class PredictionAnswerService : IPredictionAnswerService
{
    // Cambiamos a determinístico para evitar que el LLM alucine fechas y asegurar
    // el tono exacto que solicitaste (claro, directo y advirtiendo el periodo).
    public Task<string> HumanizeAsync(PredictionIntent intent, CancellationToken ct)
    {
        if (!intent.IsPredictionRequest)
            return Task.FromResult("No detecté una solicitud de pronóstico válida.");

        // Si el router detectó que piden "hoy" o "esta semana", devolvemos la razón.
        if (!intent.IsSupportedByCurrentModel)
            return Task.FromResult($"No puedo procesar esta solicitud: {intent.UnsupportedReason ?? "Periodo no soportado por el modelo."}");

        if (!intent.IsSuccess || intent.PredictedValue == null)
            return Task.FromResult($"No pude generar el pronóstico. {intent.UnsupportedReason}");

        var qty = Math.Round(intent.PredictedValue.Value, 0);
        var period = intent.ForecastPeriodLabel ?? "el próximo mes";

        // Tono estricto y explícito solicitado
        var msg = $"Pronóstico de scrap para el N/P {intent.EntityName} en {period}: {qty.ToString("N0", new CultureInfo("es-MX"))} piezas estimadas. " +
                  "Este valor corresponde al mes calendario futuro completo, no a hoy ni al acumulado actual.";

        intent.HumanizedMessage = msg;
        return Task.FromResult(msg);
    }
}