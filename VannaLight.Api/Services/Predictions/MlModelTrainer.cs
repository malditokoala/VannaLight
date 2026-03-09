using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace VannaLight.Api.Services.Predictions;

// 1. Clases internas para que ML.NET entienda los datos (Entrada y Salida)
public class ModelInput
{
    public string ProductName { get; set; } = string.Empty;
    public float Month { get; set; }
    public float Sales { get; set; }
}

public class ModelOutput
{
    [ColumnName("Score")]
    public float PredictedSales { get; set; }
}

// 2. El Entrenador (Se encarga de conectarse a SQL y crear el modelo predictivo)
public static class MlModelTrainer
{
    public static readonly string ModelPath = Path.Combine(Environment.CurrentDirectory, "Data", "SalesForecastModel.zip");

    public static void TrainAndSaveModel(string connectionString)
    {
        var mlContext = new MLContext(seed: 0);
        Console.WriteLine("[ML.NET] Extrayendo datos históricos de SQL Server (Northwind)...");

        IEnumerable<ModelInput> trainingData;

        using (var connection = new SqlConnection(connectionString))
        {
            // CORRECCIÓN: Usamos OrderDetails sin espacios, respetando el esquema real de tu base de datos
            const string sql = @"
                SELECT 
                    p.ProductName, 
                    CAST(MONTH(o.OrderDate) AS REAL) AS [Month], 
                    CAST(SUM(od.Quantity) AS REAL) AS Sales
                FROM OrderDetails od
                INNER JOIN Orders o ON od.OrderID = o.OrderID
                INNER JOIN Products p ON od.ProductID = p.ProductID
                WHERE o.OrderDate IS NOT NULL
                GROUP BY p.ProductName, MONTH(o.OrderDate)";

            // Dapper mapea el resultado directo a ModelInput
            trainingData = connection.Query<ModelInput>(sql).ToList();
        }

        if (!trainingData.Any())
        {
            Console.WriteLine("[ML.NET ERROR] No hay datos suficientes para entrenar.");
            return;
        }

        var dataView = mlContext.Data.LoadFromEnumerable(trainingData);

        // Pipeline de Aprendizaje: Codificamos el texto y usamos el algoritmo FastTree
        var pipeline = mlContext.Transforms.Categorical.OneHotEncoding("ProductNameEncoded", "ProductName")
            .Append(mlContext.Transforms.Concatenate("Features", "ProductNameEncoded", "Month"))
            .Append(mlContext.Regression.Trainers.FastTree(labelColumnName: "Sales", featureColumnName: "Features"));

        Console.WriteLine($"[ML.NET] Entrenando modelo con {trainingData.Count()} registros...");

        // Entrenar el modelo y guardarlo físicamente en el disco
        var model = pipeline.Fit(dataView);
        Directory.CreateDirectory(Path.GetDirectoryName(ModelPath)!);
        mlContext.Model.Save(model, dataView.Schema, ModelPath);

        Console.WriteLine($"[ML.NET] ¡Modelo guardado con éxito en: {ModelPath}");
    }
}