using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;
using VannaLight.Core.Settings;

namespace VannaLight.Api.Services.Predictions;

public sealed class IndustrialDomainPackAdapter : IDomainPackProvider
{
    private readonly IMlTrainingProfileProvider _mlTrainingProfileProvider;
    private readonly ISystemConfigProvider _systemConfigProvider;

    public IndustrialDomainPackAdapter(
        IMlTrainingProfileProvider mlTrainingProfileProvider,
        ISystemConfigProvider systemConfigProvider)
    {
        _mlTrainingProfileProvider = mlTrainingProfileProvider;
        _systemConfigProvider = systemConfigProvider;
    }

    public async Task<DomainPackDefinition> GetDomainPackAsync(string domain, CancellationToken ct = default)
    {
        var normalizedDomain = string.IsNullOrWhiteSpace(domain)
            ? (await _systemConfigProvider.GetValueAsync("Retrieval", "Domain", ct: ct))?.Trim() ?? "industrial-kpi"
            : domain.Trim();

        var mlProfile = await _mlTrainingProfileProvider.GetActiveProfileAsync(ct);
        var calendarProfileKey = mlProfile.ShiftTableQualifiedName.Equals("dbo.Turnos", StringComparison.OrdinalIgnoreCase)
            ? "shift-calendar"
            : $"shift-calendar:{mlProfile.ShiftTableQualifiedName}";

        return new DomainPackDefinition
        {
            Key = $"{normalizedDomain}:industrial-adapter",
            DisplayName = $"Industrial Pack · {normalizedDomain}",
            Domain = normalizedDomain,
            ConnectionName = mlProfile.ConnectionName,
            CalendarProfileKey = calendarProfileKey,
            Description = "Adapter transicional que traduce las vistas KPI industriales actuales a una semántica analítica genérica.",
            Metrics =
            [
                new MetricDefinition
                {
                    Key = "scrap_qty",
                    DisplayName = "Scrap Qty",
                    Description = "Cantidad de scrap agregada por fecha/turno/pieza.",
                    TimeColumn = "OperationDate",
                    SqlExpression = "SUM(ISNULL(ScrapQty, 0))",
                    BaseObject = mlProfile.ScrapViewQualifiedName,
                    DefaultAggregation = "sum",
                    AllowedDimensions = ["part", "shift", "press", "department"]
                },
                new MetricDefinition
                {
                    Key = "produced_qty",
                    DisplayName = "Produced Qty",
                    Description = "Cantidad producida agregada por fecha/turno/pieza.",
                    TimeColumn = "OperationDate",
                    SqlExpression = "SUM(ISNULL(ProducedQty, 0))",
                    BaseObject = mlProfile.ProductionViewQualifiedName,
                    DefaultAggregation = "sum",
                    AllowedDimensions = ["part", "shift", "press", "department"]
                },
                new MetricDefinition
                {
                    Key = "downtime_minutes",
                    DisplayName = "Downtime Minutes",
                    Description = "Minutos de tiempo muerto agregados por fecha/turno/pieza.",
                    TimeColumn = "OperationDate",
                    SqlExpression = "SUM(ISNULL(DownTimeMinutes, 0))",
                    BaseObject = mlProfile.DowntimeViewQualifiedName,
                    DefaultAggregation = "sum",
                    AllowedDimensions = ["part", "shift", "press", "department", "failure"]
                }
            ],
            Dimensions =
            [
                new DimensionDefinition
                {
                    Key = "part",
                    DisplayName = "Part",
                    Description = "Número de parte o producto industrial.",
                    SqlExpression = "PartNumber"
                },
                new DimensionDefinition
                {
                    Key = "shift",
                    DisplayName = "Shift",
                    Description = "Turno productivo de la planta.",
                    SqlExpression = "ShiftId"
                },
                new DimensionDefinition
                {
                    Key = "press",
                    DisplayName = "Press",
                    Description = "Prensa o estación productiva.",
                    SqlExpression = "PressName"
                },
                new DimensionDefinition
                {
                    Key = "department",
                    DisplayName = "Department",
                    Description = "Departamento o área operativa.",
                    SqlExpression = "Department"
                },
                new DimensionDefinition
                {
                    Key = "failure",
                    DisplayName = "Failure",
                    Description = "Código o clasificación de falla.",
                    SqlExpression = "FailureCode"
                }
            ]
        };
    }
}
