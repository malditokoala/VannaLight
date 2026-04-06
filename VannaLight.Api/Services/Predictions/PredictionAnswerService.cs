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

        string entityLabel = string.IsNullOrWhiteSpace(intent.EntityName) ? "serie consultada" : intent.EntityName!;
        string periodLabel = string.IsNullOrWhiteSpace(intent.ForecastPeriodLabel) ? "el periodo solicitado" : intent.ForecastPeriodLabel!;
        string metricLabel = intent.MetricKey switch
        {
            "net_sales" => "ventas netas estimadas",
            "units_sold" => "unidades estimadas",
            "order_count" => "ordenes estimadas",
            "produced_qty" => "produccion estimada",
            "downtime_minutes" => "minutos estimados",
            "scrap_qty" => "scrap estimado",
            _ => "unidades objetivo"
        };

        string message = $"Pronóstico estimado para {periodLabel} - serie {entityLabel}: {qty.ToString("N0", CultureInfo.InvariantCulture)} {metricLabel}.\n\n" +
                         "Nota: Este valor representa la predicción estadística del total para el periodo indicado. La semántica exacta de la unidad depende de la métrica configurada en el perfil predictivo.";

        intent.HumanizedMessage = message;
        return Task.FromResult(message);
    }
}
