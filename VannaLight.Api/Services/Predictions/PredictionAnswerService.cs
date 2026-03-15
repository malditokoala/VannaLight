using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Api.Services.Predictions;

public class PredictionAnswerService : IPredictionAnswerService
{
    public Task<string> HumanizeAsync(PredictionIntent intent, CancellationToken ct)
    {
        if (!intent.IsPredictionRequest || !intent.IsSuccess || intent.PredictedValue == null)
            return Task.FromResult($"No se pudo generar el pronóstico. {intent.UnsupportedReason}");

        var qty = System.Math.Round(intent.PredictedValue.Value, 0);

        // Mensaje inequívoco
        string message = $"Pronóstico estimado para {intent.ForecastPeriodLabel} - N/P {intent.EntityName}: {qty.ToString("N0", CultureInfo.InvariantCulture)} piezas de scrap.\n\n" +
                         "Nota: Este valor representa la predicción estadística del total para el periodo completo indicado, no el acumulado en tiempo real.";

        intent.HumanizedMessage = message;
        return Task.FromResult(message);
    }
}