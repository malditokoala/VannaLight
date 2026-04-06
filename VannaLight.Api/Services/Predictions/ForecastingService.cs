using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.ML;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;
using VannaLight.Core.Settings;

namespace VannaLight.Api.Services.Predictions;

/// <summary>
/// Información de soporte para calcular la pertenencia de la hora actual a un turno.
/// </summary>
internal class ActiveBucketInfo
{
    public int BucketKey { get; set; }
    public string BucketLabel { get; set; } = string.Empty;
    public long BucketStartTick { get; set; }
    public long BucketEndTick { get; set; }

    public bool ContainsTime(long currentTicks)
    {
        if (BucketStartTick < BucketEndTick)
            return currentTicks >= BucketStartTick && currentTicks < BucketEndTick;
        else
            return currentTicks >= BucketStartTick || currentTicks < BucketEndTick;
    }
}

public class ForecastingService : IForecastingService
{
    private readonly MLContext _mlContext;
    private readonly IMlTrainingProfileProvider _profileProvider;
    private readonly IPredictionProfileStore _predictionProfileStore;
    private readonly SqliteOptions _sqliteOptions;
    private ITransformer? _model;
    private MlTrainingProfile? _activeProfile;
    private PredictionProfile? _activePredictionProfile;
    private string? _connectionString;

    public ForecastingService(
        IMlTrainingProfileProvider profileProvider,
        IPredictionProfileStore predictionProfileStore,
        SqliteOptions sqliteOptions)
    {
        _mlContext = new MLContext(seed: 0);
        _profileProvider = profileProvider;
        _predictionProfileStore = predictionProfileStore;
        _sqliteOptions = sqliteOptions;
    }

    private async Task EnsureModelExistsAndLoadAsync(AskExecutionContext executionContext, PredictionIntent intent, CancellationToken ct = default)
    {
        _activePredictionProfile = await ResolvePredictionProfileAsync(executionContext, intent, ct);
        _activeProfile = await BuildEffectiveTrainingProfileAsync(_activePredictionProfile, ct);
        _connectionString = await _profileProvider.ResolveConnectionStringAsync(_activeProfile, ct);

        if (!File.Exists(MlModelTrainer.ModelPath) || !MlModelTrainer.IsModelAlignedWithProfile(_activeProfile))
            MlModelTrainer.TrainAndSaveModel(_connectionString, _activeProfile);

        _model = _mlContext.Model.Load(MlModelTrainer.ModelPath, out _);
    }

    public async Task<PredictionIntent> PredictAsync(PredictionIntent intent, AskExecutionContext executionContext, CancellationToken ct = default)
    {
        if (!intent.IsPredictionRequest || !intent.IsSupportedByCurrentModel)
        {
            intent.IsSuccess = false;
            return intent;
        }

        await EnsureModelExistsAndLoadAsync(executionContext, intent, ct);

        if (string.IsNullOrWhiteSpace(intent.EntityName) || _model == null || _activeProfile is null || string.IsNullOrWhiteSpace(_connectionString))
        {
            intent.IsSuccess = false;
            intent.UnsupportedReason = "Serie objetivo invalida o motor ML no inicializado.";
            return intent;
        }

        try
        {
            var seriesKey = intent.EntityName.Trim();

            // 1. Obtener la definicion de buckets activos de la serie
            var activeBuckets = LoadActiveBuckets();
            if (activeBuckets.Count == 0)
                throw new InvalidOperationException("No hay buckets temporales configurados para la serie.");

            // 2. Determinar el contexto temporal actual
            var now = DateTime.Now;
            var currentTicks = now.TimeOfDay.Ticks;
            var currentBucket = activeBuckets.FirstOrDefault(s => s.ContainsTime(currentTicks)) ?? activeBuckets.First();

            var currentObservedOn = (currentBucket.BucketStartTick > currentBucket.BucketEndTick && currentTicks < currentBucket.BucketEndTick)
                ? now.Date.AddDays(-1)
                : now.Date;

            var rawHistory = LoadSeriesHistory(seriesKey);
            if (rawHistory.Count == 0)
            {
                intent.IsSuccess = false;
                intent.UnsupportedReason = $"No hay historial para la serie {seriesKey}.";
                return intent;
            }

            // 4. Definir pasos de predicción según el Target del Router
            int shiftsToPredict = intent.PredictionTarget switch
            {
                "EndOfCurrentShift" => 1,
                "NextShift" => 2,
                "Tomorrow" => activeBuckets.Count + 1,
                "NextMonth" => activeBuckets.Count * 30,
                _ => 1
            };

            var firstRecord = rawHistory.First();
            var historyDict = rawHistory.ToDictionary(x => $"{x.ObservedOn:yyyyMMdd}-{x.BucketKey}", x => x.TargetValue);
            var continuousSeries = new List<float>();

            DateTime iterDate = firstRecord.ObservedOn;
            bool reachedTarget = false;

            while (!reachedTarget)
            {
                foreach (var s in activeBuckets)
                {
                    if (iterDate == currentObservedOn && s.BucketKey == currentBucket.BucketKey)
                    {
                        reachedTarget = true;
                        break;
                    }
                    var key = $"{iterDate:yyyyMMdd}-{s.BucketKey}";
                    continuousSeries.Add(historyDict.ContainsKey(key) ? historyDict[key] : 0);
                }
                if (!reachedTarget) iterDate = iterDate.AddDays(1);
            }

            intent.HistoryShiftsUsed = continuousSeries.Count; // Compatibilidad temporal de nombre

            var engine = _mlContext.Model.CreatePredictionEngine<ForecastModelInput, ForecastModelOutput>(_model);
            float accumulatedValue = 0;
            DateTime predictDate = currentObservedOn;
            int predictBucketIndex = activeBuckets.FindIndex(s => s.BucketKey == currentBucket.BucketKey);

            for (int step = 1; step <= shiftsToPredict; step++)
            {
                var targetBucket = activeBuckets[predictBucketIndex];
                int dow = predictDate.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)predictDate.DayOfWeek;

                var input = new ForecastModelInput
                {
                    SeriesKey = seriesKey,
                    BucketKey = targetBucket.BucketKey,
                    DayOfWeekIso = dow,
                    Lag1Value = continuousSeries.LastOrDefault(),
                    Avg3Value = continuousSeries.Count >= 3 ? continuousSeries.TakeLast(3).Average() : continuousSeries.LastOrDefault(),
                    TargetValue = 0
                };

                var prediction = engine.Predict(input);
                float val = Math.Max(0, prediction.PredictedValue);
                continuousSeries.Add(val);

                // Lógica de acumulación por target temporal
                if (intent.PredictionTarget == "EndOfCurrentShift" && step == 1) accumulatedValue = val;
                else if (intent.PredictionTarget == "NextShift" && step == 2) accumulatedValue = val;
                else if (intent.PredictionTarget == "Tomorrow" && predictDate == now.Date.AddDays(1)) accumulatedValue += val;
                else if (intent.PredictionTarget == "NextMonth") accumulatedValue += val;

                // Avanzar al siguiente bucket
                predictBucketIndex++;
                if (predictBucketIndex >= activeBuckets.Count)
                {
                    predictBucketIndex = 0;
                    predictDate = predictDate.AddDays(1);
                }
            }

            // 7. Resultado
            intent.IsSuccess = true;
            intent.PredictedValue = accumulatedValue;
            intent.ForecastPeriodLabel = intent.PredictionTarget switch
            {
                "EndOfCurrentShift" => $"Cierre de {currentBucket.BucketLabel} ({currentObservedOn:dd/MMM})",
                "NextShift" => "Proximo bucket disponible",
                "Tomorrow" => $"Dia completo {now.AddDays(1):dd/MMM}",
                _ => "Periodo proyectado"
            };

            return intent;
        }
        catch (Exception ex)
        {
            intent.IsSuccess = false;
            intent.UnsupportedReason = "Fallo interno en el motor de prediccion.";
            return intent;
        }
    }

    private List<ActiveBucketInfo> LoadActiveBuckets()
    {
        if (_activeProfile is null || string.IsNullOrWhiteSpace(_connectionString))
            return new List<ActiveBucketInfo>();

        using var conn = new SqlConnection(_connectionString);
        return conn.Query<ActiveBucketInfo>(MlTrainingSqlBuilder.BuildActiveShiftsSql(_activeProfile)).ToList();
    }

    private List<TemporalObservationRow> LoadSeriesHistory(string seriesKey)
    {
        if (_activeProfile is null || string.IsNullOrWhiteSpace(_connectionString))
            return new List<TemporalObservationRow>();

        using var connection = new SqlConnection(_connectionString);
        return connection.Query<TemporalObservationRow>(MlTrainingSqlBuilder.BuildShiftHistorySql(_activeProfile), new { SeriesKey = seriesKey.Trim() }).ToList();
    }

    private async Task<PredictionProfile?> ResolvePredictionProfileAsync(AskExecutionContext executionContext, PredictionIntent intent, CancellationToken ct)
    {
        var domain = string.IsNullOrWhiteSpace(executionContext?.Domain) ? "erp-kpi-pilot" : executionContext.Domain.Trim();
        var profiles = await _predictionProfileStore.GetAllAsync(_sqliteOptions.DbPath, domain, ct);
        if (profiles.Count == 0)
            return null;

        var normalizedConnection = string.IsNullOrWhiteSpace(executionContext?.ConnectionName)
            ? string.Empty
            : executionContext.ConnectionName.Trim();
        var metricKey = string.IsNullOrWhiteSpace(intent?.MetricKey)
            ? string.Empty
            : intent.MetricKey.Trim();
        var seriesType = string.IsNullOrWhiteSpace(intent?.SeriesType)
            ? string.Empty
            : intent.SeriesType.Trim();

        static bool ProfileSupportsSeriesType(PredictionProfile profile, string seriesType)
        {
            if (string.IsNullOrWhiteSpace(seriesType))
                return false;

            if (string.IsNullOrWhiteSpace(profile.GroupByJson))
                return false;

            try
            {
                var values = JsonSerializer.Deserialize<string[]>(profile.GroupByJson) ?? Array.Empty<string>();
                return values.Any(x => string.Equals(x?.Trim(), seriesType, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return profile.GroupByJson.Contains(seriesType, StringComparison.OrdinalIgnoreCase);
            }
        }

        static int ScoreProfile(PredictionProfile profile, string connectionName, string metricKey, string seriesType)
        {
            var score = 0;

            if (profile.IsActive)
                score += 100;

            if (!string.IsNullOrWhiteSpace(metricKey) &&
                string.Equals(profile.TargetMetricKey?.Trim(), metricKey, StringComparison.OrdinalIgnoreCase))
            {
                score += 40;
            }

            if (ProfileSupportsSeriesType(profile, seriesType))
                score += 30;

            if (!string.IsNullOrWhiteSpace(connectionName) &&
                !string.IsNullOrWhiteSpace(profile.ConnectionName) &&
                string.Equals(profile.ConnectionName.Trim(), connectionName, StringComparison.OrdinalIgnoreCase))
            {
                score += 25;
            }

            if (string.Equals(profile.SourceMode, MlTrainingProfile.CustomSqlMode, StringComparison.OrdinalIgnoreCase))
                score += 5;

            return score;
        }

        return profiles
            .OrderByDescending(profile => ScoreProfile(profile, normalizedConnection, metricKey, seriesType))
            .ThenBy(profile => profile.ProfileKey, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private async Task<MlTrainingProfile> BuildEffectiveTrainingProfileAsync(PredictionProfile? predictionProfile, CancellationToken ct)
    {
        var baseProfile = await _profileProvider.GetActiveProfileAsync(ct);
        if (predictionProfile is null)
            return baseProfile;

        if (string.Equals(predictionProfile.SourceMode, MlTrainingProfile.CustomSqlMode, StringComparison.OrdinalIgnoreCase))
        {
            return new MlTrainingProfile
            {
                ProfileName = predictionProfile.ProfileKey,
                DisplayName = predictionProfile.DisplayName,
                SourceMode = MlTrainingProfile.CustomSqlMode,
                ConnectionName = string.IsNullOrWhiteSpace(predictionProfile.ConnectionName) ? baseProfile.ConnectionName : predictionProfile.ConnectionName,
                Description = predictionProfile.Notes,
                TrainingSql = predictionProfile.TargetSeriesSource
            };
        }

        return new MlTrainingProfile
        {
            ProfileName = predictionProfile.ProfileKey,
            DisplayName = predictionProfile.DisplayName,
            SourceMode = MlTrainingProfile.KpiViewsMode,
            ConnectionName = string.IsNullOrWhiteSpace(predictionProfile.ConnectionName) ? baseProfile.ConnectionName : predictionProfile.ConnectionName,
            Description = predictionProfile.Notes,
            ShiftTableName = baseProfile.ShiftTableName,
            ProductionViewName = baseProfile.ProductionViewName,
            ScrapViewName = baseProfile.ScrapViewName,
            DowntimeViewName = baseProfile.DowntimeViewName
        };
    }
}









