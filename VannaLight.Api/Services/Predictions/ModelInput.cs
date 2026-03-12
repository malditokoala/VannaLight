using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers.FastTree;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VannaLight.Api.Services.Predictions;

// 1. Clases de Datos
public sealed class ModelInput
{
    public string PartNumber { get; set; } = string.Empty;
    public float MonthNumber { get; set; }
    public float YearMonthIndex { get; set; }

    // Ingeniería de características (Features) para series de tiempo
    public float Lag1ScrapQty { get; set; } // Scrap del mes anterior
    public float Avg3ScrapQty { get; set; } // Promedio de scrap de los últimos 3 meses

    // Etiqueta a predecir
    public float ScrapQty { get; set; }
}

public sealed class ModelOutput
{
    [ColumnName("Score")]
    public float PredictedScrapQty { get; set; }
}

internal sealed class MonthlyScrapRow
{
    public string PartNumber { get; set; } = string.Empty;
    public int YearNumber { get; set; }
    public int MonthNumber { get; set; }
    public int YearMonthIndex { get; set; }
    public float ScrapQty { get; set; }
}

public static class MlModelTrainer
{
    public static readonly string ModelPath = Path.Combine(Environment.CurrentDirectory, "Data", "Models", "ScrapForecastModel_v2.zip");

    public static void TrainAndSaveModel(string connectionString)
    {
        var mlContext = new MLContext(seed: 0);
        Console.WriteLine("[ML.NET] Extrayendo dataset mensual de scrap desde las vistas KPI del ERP...");

        List<MonthlyScrapRow> rawRows;

        using (var connection = new SqlConnection(connectionString))
        {
            // Extraemos el historial combinando producción y scrap por Número de Parte y Mes
            const string sql = @"
            WITH ProductionMonthly AS
            (
                SELECT 
                    PartNumber, YearNumber, MonthNumber,
                    (YearNumber * 12 + MonthNumber) AS YearMonthIndex
                FROM dbo.vw_KpiProduction_v1
                WHERE OperationDate IS NOT NULL AND PartNumber IS NOT NULL
                GROUP BY PartNumber, YearNumber, MonthNumber
            ),
            ScrapMonthly AS
            (
                SELECT 
                    PartNumber, YearNumber, MonthNumber,
                    SUM(CAST(ISNULL(ScrapQty, 0) AS float)) AS ScrapQty
                FROM dbo.vw_KpiScrap_v1
                WHERE OperationDate IS NOT NULL AND PartNumber IS NOT NULL
                GROUP BY PartNumber, YearNumber, MonthNumber
            )
            SELECT 
                p.PartNumber, p.YearNumber, p.MonthNumber, p.YearMonthIndex,
                CAST(ISNULL(s.ScrapQty, 0) AS float) AS ScrapQty
            FROM ProductionMonthly p
            LEFT JOIN ScrapMonthly s 
                ON s.PartNumber = p.PartNumber AND s.YearNumber = p.YearNumber AND s.MonthNumber = p.MonthNumber
            ORDER BY p.PartNumber, p.YearMonthIndex;";

            rawRows = connection.Query<MonthlyScrapRow>(sql).ToList();
        }

        if (rawRows.Count == 0)
        {
            Console.WriteLine("[ML.NET ERROR] No hay datos suficientes en las vistas para entrenar.");
            return;
        }

        // Construir el dataset calculando los rezagos (Lags)
        var trainingRows = BuildTrainingRows(rawRows);

        if (trainingRows.Count < 10)
        {
            Console.WriteLine($"[ML.NET WARNING] Muy pocos ejemplos históricos ({trainingRows.Count}). El modelo podría ser impreciso.");
            // No hacemos return para permitir que entrene al menos con un dataset pequeño en desarrollo
        }

        var dataView = mlContext.Data.LoadFromEnumerable(trainingRows);

        // 2. Pipeline de Transformación y Entrenamiento (FastTree)
        var pipeline = mlContext.Transforms.Categorical.OneHotEncoding(
                outputColumnName: "PartNumberEncoded",
                inputColumnName: nameof(ModelInput.PartNumber))
            .Append(mlContext.Transforms.Concatenate(
                "Features",
                "PartNumberEncoded",
                nameof(ModelInput.MonthNumber),
                nameof(ModelInput.YearMonthIndex),
                nameof(ModelInput.Lag1ScrapQty),
                nameof(ModelInput.Avg3ScrapQty)))
            .Append(mlContext.Regression.Trainers.FastTree(
                new FastTreeRegressionTrainer.Options
                {
                    LabelColumnName = nameof(ModelInput.ScrapQty),
                    FeatureColumnName = "Features",
                    NumberOfTrees = 150, // Ajustado para evitar sobreajuste en datasets medianos
                    NumberOfLeaves = 30,
                    MinimumExampleCountPerLeaf = 3,
                    LearningRate = 0.1
                }));

        Console.WriteLine($"[ML.NET] Entrenando modelo FastTree con {trainingRows.Count} puntos de datos mensuales...");
        var model = pipeline.Fit(dataView);

        Directory.CreateDirectory(Path.GetDirectoryName(ModelPath)!);
        mlContext.Model.Save(model, dataView.Schema, ModelPath);

        Console.WriteLine($"[ML.NET] ¡Modelo guardado con éxito en: {ModelPath}");
    }

    internal static List<ModelInput> BuildTrainingRows(List<MonthlyScrapRow> rawRows)
    {
        var result = new List<ModelInput>();
        var groups = rawRows.GroupBy(x => x.PartNumber).ToList();

        foreach (var group in groups)
        {
            var ordered = group.OrderBy(x => x.YearMonthIndex).ToList();
            if (ordered.Count < 2) continue; // Necesitamos al menos 2 meses para calcular un rezago

            for (int i = 1; i < ordered.Count; i++)
            {
                var history = ordered.Take(i).ToList();
                var current = ordered[i];

                result.Add(new ModelInput
                {
                    PartNumber = current.PartNumber,
                    MonthNumber = current.MonthNumber,
                    YearMonthIndex = current.YearMonthIndex,
                    Lag1ScrapQty = history.Last().ScrapQty,
                    Avg3ScrapQty = history.TakeLast(3).Average(x => x.ScrapQty),
                    ScrapQty = current.ScrapQty
                });
            }
        }
        return result;
    }
}