using Microsoft.ML.Data;
using System;

namespace VannaLight.Api.Services.Predictions;

// Canonical forecasting contracts for any time series domain.
public sealed class ForecastModelInput
{
    public string SeriesKey { get; set; } = string.Empty;
    public float BucketKey { get; set; }
    public float DayOfWeekIso { get; set; }
    public float Lag1Value { get; set; }
    public float Avg3Value { get; set; }
    public float TargetValue { get; set; }
}

public sealed class ForecastModelOutput
{
    [ColumnName("Score")]
    public float PredictedValue { get; set; }
}

internal sealed class TemporalObservationRow
{
    public string SeriesKey { get; set; } = string.Empty;
    public DateTime ObservedOn { get; set; }
    public int BucketKey { get; set; }
    public string BucketLabel { get; set; } = string.Empty;
    public long BucketStartTick { get; set; }
    public long BucketEndTick { get; set; }
    public float TargetValue { get; set; }
}
