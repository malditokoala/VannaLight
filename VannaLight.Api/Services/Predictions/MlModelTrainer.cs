using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VannaLight.Infrastructure.MachineLearning;

// 1. Clases internas adaptadas a la Planta
public class ModelInput
{
    public string PartNumber { get; set; } = string.Empty;
    public float Month { get; set; }
    public float ScrapQty { get; set; }
}

public class ModelOutput
{
    [ColumnName("Score")]
    public float PredictedScrap { get; set; }
}

public static class MlModelTrainer
{
    // Cambiamos el nombre del archivo para no chocar con el viejo
    public static readonly string ModelPath = Path.Combine(Environment.CurrentDirectory, "Data", "ScrapForecastModel.zip");

    public static void TrainAndSaveModel(string connectionString)
    {
        var mlContext = new MLContext(seed: 0);
        Console.WriteLine("[ML.NET] Extrayendo datos históricos de Scrap desde vw_KpiScrap_v1...");

        IEnumerable<ModelInput> trainingData;

        using (var connection = new SqlConnection(connectionString))
        {
            // QUERY REAL: Agrupamos el scrap por Número de Parte y Mes usando tu vista
            const string sql = @"
                SELECT 
                    PartNumber, 
                    CAST(MonthNumber AS REAL) AS [Month], 
                    CAST(SUM(ScrapQty) AS REAL) AS ScrapQty
                FROM dbo.vw_KpiScrap_v1
                WHERE OperationDate IS NOT NULL AND PartNumber IS NOT NULL
                GROUP BY PartNumber, MonthNumber";

            trainingData = connection.Query<ModelInput>(sql).ToList();
        }

        if (!trainingData.Any())
        {
            Console.WriteLine("[ML.NET ERROR] No hay datos de Scrap suficientes para entrenar en la vista.");
            return;
        }

        var dataView = mlContext.Data.LoadFromEnumerable(trainingData);

        // 2. Pipeline de Aprendizaje: Codificamos el N/P y usamos FastTree para regresión
        var pipeline = mlContext.Transforms.Categorical.OneHotEncoding("PartNumberEncoded", "PartNumber")
            .Append(mlContext.Transforms.Concatenate("Features", "PartNumberEncoded", "Month"))
            .Append(mlContext.Regression.Trainers.FastTree(labelColumnName: "ScrapQty", featureColumnName: "Features"));

        Console.WriteLine($"[ML.NET] Entrenando modelo predictivo con {trainingData.Count()} meses/partes históricos...");

        // 3. Entrenar y Guardar
        var model = pipeline.Fit(dataView);
        Directory.CreateDirectory(Path.GetDirectoryName(ModelPath)!);
        mlContext.Model.Save(model, dataView.Schema, ModelPath);

        Console.WriteLine($"[ML.NET] ¡Modelo de Planta guardado con éxito en: {ModelPath}");
    }
}