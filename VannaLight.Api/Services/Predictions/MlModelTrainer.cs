using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.ML;
using Microsoft.ML.Trainers.FastTree;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using VannaLight.Core.Settings;

namespace VannaLight.Api.Services.Predictions;

public static class MlModelTrainer
{
    public static readonly string ModelPath = Path.Combine(Environment.CurrentDirectory, "Data", "Models", "ScrapShiftForecast_v1.zip");
    public static readonly string MetadataPath = Path.Combine(Environment.CurrentDirectory, "Data", "Models", "ScrapShiftForecast_v1.meta.json");

    public static void TrainAndSaveModel(string connectionString, MlTrainingProfile profile)
    {
        var mlContext = new MLContext(seed: 0);
        Console.WriteLine("[ML.NET] Extrayendo dataset SHIFT-LEVEL de scrap...");

        List<TemporalObservationRow> rawRows;

        using (var connection = new SqlConnection(connectionString))
        {
            var sql = MlTrainingSqlBuilder.BuildTrainingDatasetSql(profile);
            rawRows = connection.Query<TemporalObservationRow>(sql).ToList();
        }

        if (rawRows.Count == 0) return;

        var trainingRows = BuildTrainingRows(rawRows);
        var dataView = mlContext.Data.LoadFromEnumerable(trainingRows);

        var pipeline = mlContext.Transforms.Categorical.OneHotEncoding("SeriesKeyEncoded", nameof(ForecastModelInput.SeriesKey))
            .Append(mlContext.Transforms.Concatenate("Features",
                "SeriesKeyEncoded", nameof(ForecastModelInput.BucketKey), nameof(ForecastModelInput.DayOfWeekIso),
                nameof(ForecastModelInput.Lag1Value), nameof(ForecastModelInput.Avg3Value)))
            .Append(mlContext.Regression.Trainers.FastTree(
                new FastTreeRegressionTrainer.Options
                {
                    LabelColumnName = nameof(ForecastModelInput.TargetValue),
                    FeatureColumnName = "Features",
                    NumberOfTrees = 200,
                    NumberOfLeaves = 40,
                    LearningRate = 0.1
                }));

        var model = pipeline.Fit(dataView);
        Directory.CreateDirectory(Path.GetDirectoryName(ModelPath)!);
        mlContext.Model.Save(model, dataView.Schema, ModelPath);
        SaveMetadata(profile);
    }

    public static MlModelArtifactMetadata? LoadMetadata()
    {
        if (!File.Exists(MetadataPath))
            return null;

        try
        {
            var json = File.ReadAllText(MetadataPath);
            return JsonSerializer.Deserialize<MlModelArtifactMetadata>(json);
        }
        catch
        {
            return null;
        }
    }

    public static bool IsModelAlignedWithProfile(MlTrainingProfile profile)
    {
        var metadata = LoadMetadata();
        return metadata is not null
            && !string.IsNullOrWhiteSpace(metadata.ProfileSignature)
            && string.Equals(metadata.ProfileSignature, profile.GetSignature(), StringComparison.OrdinalIgnoreCase);
    }

    private static void SaveMetadata(MlTrainingProfile profile)
    {
        var metadata = new MlModelArtifactMetadata
        {
            ProfileSignature = profile.GetSignature(),
            TrainedUtc = DateTime.UtcNow.ToString("O"),
            SourceMode = profile.NormalizedSourceMode,
            ConnectionName = profile.ConnectionName,
            DisplayName = profile.DisplayName
        };

        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        Directory.CreateDirectory(Path.GetDirectoryName(MetadataPath)!);
        File.WriteAllText(MetadataPath, json);
    }

    internal static List<ForecastModelInput> BuildTrainingRows(List<TemporalObservationRow> rawRows)
    {
        var result = new List<ForecastModelInput>();
        var groups = rawRows.GroupBy(x => x.SeriesKey).ToList();

        var availableBuckets = rawRows.Select(x => new { x.BucketKey, x.BucketStartTick, x.BucketLabel, x.BucketEndTick }).Distinct().OrderBy(x => x.BucketStartTick).ToList();

        foreach (var group in groups)
        {
            var ordered = group.OrderBy(x => x.ObservedOn).ThenBy(x => x.BucketStartTick).ToList();
            if (ordered.Count == 0) continue;

            var firstDate = ordered.First().ObservedOn;
            var lastDate = ordered.Last().ObservedOn;
            var historyDict = ordered.ToDictionary(x => $"{x.ObservedOn:yyyyMMdd}-{x.BucketKey}", x => x.TargetValue);

            var continuousHistory = new List<TemporalObservationRow>();

            for (var d = firstDate; d <= lastDate; d = d.AddDays(1))
            {
                foreach (var bucket in availableBuckets)
                {
                    var key = $"{d:yyyyMMdd}-{bucket.BucketKey}";
                    continuousHistory.Add(new TemporalObservationRow
                    {
                        SeriesKey = ordered.First().SeriesKey,
                        ObservedOn = d,
                        BucketKey = bucket.BucketKey,
                        BucketLabel = bucket.BucketLabel,
                        BucketStartTick = bucket.BucketStartTick,
                        BucketEndTick = bucket.BucketEndTick,
                        TargetValue = historyDict.ContainsKey(key) ? historyDict[key] : 0
                    });
                }
            }

            for (int i = 1; i < continuousHistory.Count; i++)
            {
                var history = continuousHistory.Take(i).ToList();
                var current = continuousHistory[i];

                // DayOfWeekIso (1=Lunes, 7=Domingo)
                int dow = current.ObservedOn.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)current.ObservedOn.DayOfWeek;

                result.Add(new ForecastModelInput
                {
                    SeriesKey = current.SeriesKey,
                    BucketKey = current.BucketKey,
                    DayOfWeekIso = dow,
                    Lag1Value = history.Last().TargetValue,
                    Avg3Value = history.TakeLast(3).Average(x => x.TargetValue),
                    TargetValue = current.TargetValue
                });
            }
        }
        return result;
    }
}
