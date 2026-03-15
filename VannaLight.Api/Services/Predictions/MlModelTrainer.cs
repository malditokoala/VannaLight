using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.ML;
using Microsoft.ML.Trainers.FastTree;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VannaLight.Api.Services.Predictions;

public static class MlModelTrainer
{
    public static readonly string ModelPath = Path.Combine(Environment.CurrentDirectory, "Data", "Models", "ScrapShiftForecast_v1.zip");

    public static void TrainAndSaveModel(string connectionString)
    {
        var mlContext = new MLContext(seed: 0);
        Console.WriteLine("[ML.NET] Extrayendo dataset SHIFT-LEVEL de scrap...");

        List<ShiftScrapRow> rawRows;

        using (var connection = new SqlConnection(connectionString))
        {
            // OBTENEMOS TURNOS ACTIVOS Y CURAMOS LOS DATOS EN UNA SOLA CONSULTA
            const string sql = @"
            WITH TurnosActivos AS (
                SELECT Id AS ShiftId, nombre AS ShiftName, inicio AS TicksInicio, fin AS TicksFin
                FROM [dbo].[Turnos]
                WHERE disponibleProduccion = 1
            ),
            BaseTimeline AS (
                SELECT LTRIM(RTRIM(p.PartNumber)) AS PartNumber, p.OperationDate, p.ShiftId
                FROM dbo.vw_KpiProduction_v1 p
                JOIN TurnosActivos t ON p.ShiftId = t.ShiftId
                WHERE p.OperationDate IS NOT NULL AND p.PartNumber IS NOT NULL
                UNION
                SELECT LTRIM(RTRIM(s.PartNumber)) AS PartNumber, s.OperationDate, s.ShiftId
                FROM dbo.vw_KpiScrap_v1 s
                JOIN TurnosActivos t ON s.ShiftId = t.ShiftId
                WHERE s.OperationDate IS NOT NULL AND s.PartNumber IS NOT NULL
            ),
            ScrapShift AS (
                SELECT LTRIM(RTRIM(s.PartNumber)) AS PartNumber, s.OperationDate, s.ShiftId, 
                       SUM(CAST(ISNULL(s.ScrapQty, 0) AS float)) AS ScrapQty
                FROM dbo.vw_KpiScrap_v1 s
                JOIN TurnosActivos t ON s.ShiftId = t.ShiftId
                WHERE s.OperationDate IS NOT NULL AND s.PartNumber IS NOT NULL
                GROUP BY LTRIM(RTRIM(s.PartNumber)), s.OperationDate, s.ShiftId
            )
            SELECT 
                b.PartNumber, 
                b.OperationDate, 
                b.ShiftId, 
                t.ShiftName,
                t.TicksInicio,
                t.TicksFin,
                CAST(ISNULL(s.ScrapQty, 0) AS float) AS ScrapQty
            FROM BaseTimeline b
            JOIN TurnosActivos t ON b.ShiftId = t.ShiftId
            LEFT JOIN ScrapShift s ON b.PartNumber = s.PartNumber AND b.OperationDate = s.OperationDate AND b.ShiftId = s.ShiftId
            ORDER BY b.PartNumber, b.OperationDate, t.TicksInicio;";

            rawRows = connection.Query<ShiftScrapRow>(sql).ToList();
        }

        if (rawRows.Count == 0) return;

        var trainingRows = BuildTrainingRows(rawRows);
        var dataView = mlContext.Data.LoadFromEnumerable(trainingRows);

        var pipeline = mlContext.Transforms.Categorical.OneHotEncoding("PartNumberEncoded", nameof(ModelInput.PartNumber))
            .Append(mlContext.Transforms.Concatenate("Features",
                "PartNumberEncoded", nameof(ModelInput.ShiftId), nameof(ModelInput.DayOfWeekIso),
                nameof(ModelInput.Lag1ScrapQty), nameof(ModelInput.Avg3ScrapQty)))
            .Append(mlContext.Regression.Trainers.FastTree(
                new FastTreeRegressionTrainer.Options
                {
                    LabelColumnName = nameof(ModelInput.ScrapQty),
                    FeatureColumnName = "Features",
                    NumberOfTrees = 200,
                    NumberOfLeaves = 40,
                    LearningRate = 0.1
                }));

        var model = pipeline.Fit(dataView);
        Directory.CreateDirectory(Path.GetDirectoryName(ModelPath)!);
        mlContext.Model.Save(model, dataView.Schema, ModelPath);
    }

    internal static List<ModelInput> BuildTrainingRows(List<ShiftScrapRow> rawRows)
    {
        var result = new List<ModelInput>();
        var groups = rawRows.GroupBy(x => x.PartNumber).ToList();

        // Obtenemos los turnos distintos para saber cómo rellenar huecos
        var availableShifts = rawRows.Select(x => new { x.ShiftId, x.TicksInicio }).Distinct().OrderBy(x => x.TicksInicio).ToList();

        foreach (var group in groups)
        {
            var ordered = group.OrderBy(x => x.OperationDate).ThenBy(x => x.TicksInicio).ToList();
            if (ordered.Count == 0) continue;

            var firstDate = ordered.First().OperationDate;
            var lastDate = ordered.Last().OperationDate;
            var historyDict = ordered.ToDictionary(x => $"{x.OperationDate:yyyyMMdd}-{x.ShiftId}", x => x.ScrapQty);

            var continuousHistory = new List<ShiftScrapRow>();

            // Rellenado de huecos estricto por Turno
            for (var d = firstDate; d <= lastDate; d = d.AddDays(1))
            {
                foreach (var shift in availableShifts)
                {
                    var key = $"{d:yyyyMMdd}-{shift.ShiftId}";
                    continuousHistory.Add(new ShiftScrapRow
                    {
                        PartNumber = ordered.First().PartNumber,
                        OperationDate = d,
                        ShiftId = shift.ShiftId,
                        ScrapQty = historyDict.ContainsKey(key) ? historyDict[key] : 0
                    });
                }
            }

            for (int i = 1; i < continuousHistory.Count; i++)
            {
                var history = continuousHistory.Take(i).ToList();
                var current = continuousHistory[i];

                // DayOfWeekIso (1=Lunes, 7=Domingo)
                int dow = current.OperationDate.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)current.OperationDate.DayOfWeek;

                result.Add(new ModelInput
                {
                    PartNumber = current.PartNumber,
                    ShiftId = current.ShiftId,
                    DayOfWeekIso = dow,
                    Lag1ScrapQty = history.Last().ScrapQty,
                    Avg3ScrapQty = history.TakeLast(3).Average(x => x.ScrapQty),
                    ScrapQty = current.ScrapQty
                });
            }
        }
        return result;
    }
}