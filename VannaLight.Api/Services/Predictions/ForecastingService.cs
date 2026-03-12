using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.ML;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Api.Services.Predictions;

internal sealed class MonthlyHistoryPoint
{
    public string PartNumber { get; set; } = string.Empty;
    public int YearNumber { get; set; }
    public int MonthNumber { get; set; }
    public int YearMonthIndex { get; set; }
    public float ScrapQty { get; set; }
}

public class ForecastingService : IForecastingService
{
    private readonly MLContext _mlContext;
    private readonly string _connectionString;
    private ITransformer? _model;

    public ForecastingService(IConfiguration config)
    {
        _mlContext = new MLContext(seed: 0);
        _connectionString = config.GetConnectionString("OperationalDb")
            ?? throw new Exception("No connection string found.");

        EnsureModelExistsAndLoad();
    }

    private void EnsureModelExistsAndLoad()
    {
        if (!File.Exists(MlModelTrainer.ModelPath))
        {
            MlModelTrainer.TrainAndSaveModel(_connectionString);
        }

        _model = _mlContext.Model.Load(MlModelTrainer.ModelPath, out _);
    }

    public Task<PredictionIntent> PredictAsync(PredictionIntent intent)
    {
        // 1. Barrera de Soporte: Rechazamos si el router detectó un periodo inválido
        if (!intent.IsPredictionRequest || !intent.IsSupportedByCurrentModel)
        {
            intent.IsSuccess = false;
            return Task.FromResult(intent);
        }

        if (string.IsNullOrWhiteSpace(intent.EntityName) || _model == null)
        {
            intent.IsSuccess = false;
            intent.UnsupportedReason = "No se detectó un número de parte válido.";
            return Task.FromResult(intent);
        }

        try
        {
            var horizon = intent.Horizon <= 0 ? 1 : intent.Horizon;
            var partNumber = intent.EntityName.Trim();

            var history = LoadMonthlyHistory(partNumber);

            if (history.Count == 0)
            {
                intent.IsSuccess = false;
                intent.UnsupportedReason = $"El N/P {partNumber} no tiene historial en el ERP.";
                return Task.FromResult(intent);
            }

            // Guardamos el metadato real de transparencia
            intent.HistoryMonthsUsed = history.Count;

            var engine = _mlContext.Model.CreatePredictionEngine<ModelInput, ModelOutput>(_model);

            var simulatedSeries = history.OrderBy(x => x.YearMonthIndex).Select(x => x.ScrapQty).ToList();
            var anchor = history.OrderBy(x => x.YearMonthIndex).Last();

            float predicted = 0;
            DateTime targetFutureDate = DateTime.Now;

            for (int step = 1; step <= horizon; step++)
            {
                targetFutureDate = new DateTime(anchor.YearNumber, anchor.MonthNumber, 1).AddMonths(step);
                var futureYearMonthIndex = targetFutureDate.Year * 12 + targetFutureDate.Month;

                var lag1 = simulatedSeries.Last();
                var avg3 = simulatedSeries.TakeLast(3).Average();

                var input = new ModelInput
                {
                    PartNumber = partNumber,
                    MonthNumber = targetFutureDate.Month,
                    YearMonthIndex = futureYearMonthIndex,
                    Lag1ScrapQty = lag1,
                    Avg3ScrapQty = avg3,
                    ScrapQty = 0
                };

                var prediction = engine.Predict(input);
                predicted = Math.Max(0, prediction.PredictedScrapQty);

                simulatedSeries.Add(predicted);
            }

            intent.IsSuccess = true;
            intent.PredictedValue = predicted;

            // 2. Etiqueta explícita del periodo (Ej: "abril de 2026")
            intent.ForecastPeriodLabel = targetFutureDate.ToString("MMMM 'de' yyyy", new CultureInfo("es-MX"));

            return Task.FromResult(intent);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ML.NET] Error prediciendo: {ex.Message}");
            intent.IsSuccess = false;
            intent.UnsupportedReason = "Error interno del motor predictivo.";
            return Task.FromResult(intent);
        }
    }

    private List<MonthlyHistoryPoint> LoadMonthlyHistory(string partNumber)
    {
        using var connection = new SqlConnection(_connectionString);

        // RE-APLICADO: Usamos un UNION para obtener una línea de tiempo ininterrumpida real
        // y LTRIM/RTRIM para limpiar los datos que vienen del ERP.
        const string sql = @"
            WITH BaseTimeline AS
            (
                SELECT YearNumber, MonthNumber, (YearNumber * 12 + MonthNumber) AS YearMonthIndex
                FROM dbo.vw_KpiProduction_v1
                WHERE OperationDate IS NOT NULL AND LTRIM(RTRIM(PartNumber)) = @PartNumber
                UNION
                SELECT YearNumber, MonthNumber, (YearNumber * 12 + MonthNumber) AS YearMonthIndex
                FROM dbo.vw_KpiScrap_v1
                WHERE OperationDate IS NOT NULL AND LTRIM(RTRIM(PartNumber)) = @PartNumber
            ),
            ScrapMonthly AS
            (
                SELECT YearNumber, MonthNumber, SUM(CAST(ISNULL(ScrapQty, 0) AS float)) AS ScrapQty
                FROM dbo.vw_KpiScrap_v1
                WHERE OperationDate IS NOT NULL AND LTRIM(RTRIM(PartNumber)) = @PartNumber
                GROUP BY YearNumber, MonthNumber
            )
            SELECT 
                @PartNumber AS PartNumber, 
                b.YearNumber, 
                b.MonthNumber, 
                b.YearMonthIndex,
                CAST(ISNULL(s.ScrapQty, 0) AS float) AS ScrapQty
            FROM BaseTimeline b
            LEFT JOIN ScrapMonthly s 
                ON s.YearNumber = b.YearNumber AND s.MonthNumber = b.MonthNumber
            ORDER BY b.YearMonthIndex;";

        return connection.Query<MonthlyHistoryPoint>(sql, new { PartNumber = partNumber.Trim() }).ToList();
    }
}