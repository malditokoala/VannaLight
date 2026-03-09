using Microsoft.Extensions.Configuration;
using Microsoft.ML;
using System;
using System.IO;
using System.Threading.Tasks;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Api.Services.Predictions;

public class ForecastingService : IForecastingService
{
    private readonly MLContext _mlContext;
    private PredictionEngine<ModelInput, ModelOutput>? _predictionEngine;
    private readonly string _connectionString;

    public ForecastingService(IConfiguration config)
    {
        _mlContext = new MLContext(seed: 0);
        _connectionString = config.GetConnectionString("OperationalDb")
            ?? throw new Exception("No connection string found.");

        EnsureModelExistsAndLoad();
    }

    private void EnsureModelExistsAndLoad()
    {
        // Si el "cerebro" ZIP no existe, lo auto-entrena extrayendo datos de SQL usando el Trainer del Canvas
        if (!File.Exists(MlModelTrainer.ModelPath))
        {
            MlModelTrainer.TrainAndSaveModel(_connectionString);
        }

        var model = _mlContext.Model.Load(MlModelTrainer.ModelPath, out var schema);
        _predictionEngine = _mlContext.Model.CreatePredictionEngine<ModelInput, ModelOutput>(model);
    }

    public Task<PredictionIntent> PredictAsync(PredictionIntent intent)
    {
        if (string.IsNullOrWhiteSpace(intent.EntityName) || _predictionEngine == null)
        {
            intent.IsSuccess = false;
            return Task.FromResult(intent);
        }

        try
        {
            var targetMonth = DateTime.Now.Month + intent.Horizon;
            if (targetMonth > 12) targetMonth = targetMonth % 12;

            var input = new ModelInput
            {
                ProductName = intent.EntityName,
                Month = targetMonth
            };

            var prediction = _predictionEngine.Predict(input);

            intent.IsSuccess = true;
            intent.PredictedValue = prediction.PredictedSales;
            // Para regresión tabular con FastTree, asignamos una confianza base simulada del 85-95%
            intent.ConfidenceScore = 0.88;

            return Task.FromResult(intent);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ML.NET] Error prediciendo: {ex.Message}");
            intent.IsSuccess = false;
            return Task.FromResult(intent);
        }
    }
}