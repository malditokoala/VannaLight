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

/// <summary>
/// Información de soporte para calcular la pertenencia de la hora actual a un turno.
/// </summary>
internal class ActiveShiftInfo
{
    public int ShiftId { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public long TicksInicio { get; set; }
    public long TicksFin { get; set; }

    public bool ContainsTime(long currentTicks)
    {
        if (TicksInicio < TicksFin)
            return currentTicks >= TicksInicio && currentTicks < TicksFin;
        else
            return currentTicks >= TicksInicio || currentTicks < TicksFin; // Caso: Turno Nocturno
    }
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
            ?? throw new Exception("Falta ConnectionString 'OperationalDb'.");

        EnsureModelExistsAndLoad();
    }

    private void EnsureModelExistsAndLoad()
    {
        if (!File.Exists(MlModelTrainer.ModelPath))
            MlModelTrainer.TrainAndSaveModel(_connectionString);

        _model = _mlContext.Model.Load(MlModelTrainer.ModelPath, out _);
    }

    public Task<PredictionIntent> PredictAsync(PredictionIntent intent)
    {
        if (!intent.IsPredictionRequest || !intent.IsSupportedByCurrentModel)
        {
            intent.IsSuccess = false;
            return Task.FromResult(intent);
        }

        if (string.IsNullOrWhiteSpace(intent.EntityName) || _model == null)
        {
            intent.IsSuccess = false;
            intent.UnsupportedReason = "Número de parte inválido o motor ML no inicializado.";
            return Task.FromResult(intent);
        }

        try
        {
            var partNumber = intent.EntityName.Trim();

            // 1. Obtener la definición de turnos de la planta
            var activeShifts = LoadActiveShifts();
            if (activeShifts.Count == 0) throw new Exception("No hay turnos productivos definidos en la tabla [Turnos].");

            // 2. Determinar contexto actual (Fecha Operativa y Turno Actual)
            var now = DateTime.Now;
            var currentTicks = now.TimeOfDay.Ticks;
            var currentShift = activeShifts.FirstOrDefault(s => s.ContainsTime(currentTicks)) ?? activeShifts.First();

            // Lógica de fecha operativa para turnos que cruzan medianoche
            var currentOperationDate = (currentShift.TicksInicio > currentShift.TicksFin && currentTicks < currentShift.TicksFin)
                ? now.Date.AddDays(-1)
                : now.Date;

            // 3. Cargar historial Shift-Level
            var rawHistory = LoadShiftHistory(partNumber);
            if (rawHistory.Count == 0)
            {
                intent.IsSuccess = false;
                intent.UnsupportedReason = $"No hay historial de producción/scrap para el N/P {partNumber}.";
                return Task.FromResult(intent);
            }

            // 4. Definir pasos de predicción según el Target del Router
            int shiftsToPredict = intent.PredictionTarget switch
            {
                "EndOfCurrentShift" => 1,
                "NextShift" => 2,
                "Tomorrow" => activeShifts.Count + 1,
                "NextMonth" => activeShifts.Count * 30,
                _ => 1
            };

            // 5. Rellenado de huecos (Gap-Filling) hasta el turno previo al actual
            var firstRecord = rawHistory.First();
            var historyDict = rawHistory.ToDictionary(x => $"{x.OperationDate:yyyyMMdd}-{x.ShiftId}", x => x.ScrapQty);
            var continuousSeries = new List<float>();

            DateTime iterDate = firstRecord.OperationDate;
            bool reachedTarget = false;

            while (!reachedTarget)
            {
                foreach (var s in activeShifts)
                {
                    if (iterDate == currentOperationDate && s.ShiftId == currentShift.ShiftId)
                    {
                        reachedTarget = true;
                        break;
                    }
                    var key = $"{iterDate:yyyyMMdd}-{s.ShiftId}";
                    continuousSeries.Add(historyDict.ContainsKey(key) ? historyDict[key] : 0);
                }
                if (!reachedTarget) iterDate = iterDate.AddDays(1);
            }

            intent.HistoryShiftsUsed = continuousSeries.Count;

            // 6. Inferencia Autoregresiva
            var engine = _mlContext.Model.CreatePredictionEngine<ModelInput, ModelOutput>(_model);
            float accumulatedValue = 0;
            DateTime predictDate = currentOperationDate;
            int predictShiftIndex = activeShifts.FindIndex(s => s.ShiftId == currentShift.ShiftId);

            for (int step = 1; step <= shiftsToPredict; step++)
            {
                var targetShift = activeShifts[predictShiftIndex];
                int dow = predictDate.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)predictDate.DayOfWeek;

                var input = new ModelInput
                {
                    PartNumber = partNumber,
                    ShiftId = targetShift.ShiftId,
                    DayOfWeekIso = dow,
                    Lag1ScrapQty = continuousSeries.LastOrDefault(),
                    Avg3ScrapQty = continuousSeries.Count >= 3 ? continuousSeries.TakeLast(3).Average() : continuousSeries.LastOrDefault(),
                    ScrapQty = 0
                };

                var prediction = engine.Predict(input);
                float val = Math.Max(0, prediction.PredictedScrapQty);
                continuousSeries.Add(val);

                // Lógica de acumulación por Target
                if (intent.PredictionTarget == "EndOfCurrentShift" && step == 1) accumulatedValue = val;
                else if (intent.PredictionTarget == "NextShift" && step == 2) accumulatedValue = val;
                else if (intent.PredictionTarget == "Tomorrow" && predictDate == now.Date.AddDays(1)) accumulatedValue += val;
                else if (intent.PredictionTarget == "NextMonth") accumulatedValue += val;

                // Avanzar al siguiente turno
                predictShiftIndex++;
                if (predictShiftIndex >= activeShifts.Count)
                {
                    predictShiftIndex = 0;
                    predictDate = predictDate.AddDays(1);
                }
            }

            // 7. Resultado
            intent.IsSuccess = true;
            intent.PredictedValue = accumulatedValue;
            intent.ForecastPeriodLabel = intent.PredictionTarget switch
            {
                "EndOfCurrentShift" => $"Cierre de {currentShift.ShiftName} ({currentOperationDate:dd/MMM})",
                "NextShift" => "Próximo turno disponible",
                "Tomorrow" => $"Día completo {now.AddDays(1):dd/MMM}",
                _ => "Periodo proyectado"
            };

            return Task.FromResult(intent);
        }
        catch (Exception ex)
        {
            intent.IsSuccess = false;
            intent.UnsupportedReason = "Fallo interno en el motor de predicción.";
            return Task.FromResult(intent);
        }
    }

    private List<ActiveShiftInfo> LoadActiveShifts()
    {
        using var conn = new SqlConnection(_connectionString);
        return conn.Query<ActiveShiftInfo>(@"
            SELECT Id AS ShiftId, nombre AS ShiftName, inicio AS TicksInicio, fin AS TicksFin 
            FROM [dbo].[Turnos] 
            WHERE disponibleProduccion = 1 
            ORDER BY inicio").ToList();
    }

    private List<ShiftScrapRow> LoadShiftHistory(string partNumber)
    {
        using var connection = new SqlConnection(_connectionString);
        const string sql = @"
            WITH TurnosActivos AS (
                SELECT Id AS ShiftId, inicio AS TicksInicio FROM [dbo].[Turnos] WHERE disponibleProduccion = 1
            ),
            BaseTimeline AS (
                SELECT LTRIM(RTRIM(PartNumber)) AS PartNumber, OperationDate, ShiftId
                FROM dbo.vw_KpiProduction_v1 WHERE OperationDate IS NOT NULL AND LTRIM(RTRIM(PartNumber)) = @PartNumber
                UNION
                SELECT LTRIM(RTRIM(PartNumber)) AS PartNumber, OperationDate, ShiftId
                FROM dbo.vw_KpiScrap_v1 WHERE OperationDate IS NOT NULL AND LTRIM(RTRIM(PartNumber)) = @PartNumber
            ),
            ScrapShift AS (
                SELECT LTRIM(RTRIM(PartNumber)) AS PartNumber, OperationDate, ShiftId, SUM(CAST(ISNULL(ScrapQty, 0) AS float)) AS ScrapQty
                FROM dbo.vw_KpiScrap_v1 WHERE OperationDate IS NOT NULL AND LTRIM(RTRIM(PartNumber)) = @PartNumber
                GROUP BY LTRIM(RTRIM(PartNumber)), OperationDate, ShiftId
            )
            SELECT b.PartNumber, b.OperationDate, b.ShiftId, CAST(ISNULL(s.ScrapQty, 0) AS float) AS ScrapQty
            FROM BaseTimeline b
            JOIN TurnosActivos t ON b.ShiftId = t.ShiftId
            LEFT JOIN ScrapShift s ON b.PartNumber = s.PartNumber AND b.OperationDate = s.OperationDate AND b.ShiftId = s.ShiftId
            ORDER BY b.OperationDate, t.TicksInicio;";

        return connection.Query<ShiftScrapRow>(sql, new { PartNumber = partNumber.Trim() }).ToList();
    }
}