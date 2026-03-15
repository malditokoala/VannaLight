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
    public float ShiftId { get; set; }        // Ej. 1 o 2
    public float DayOfWeekIso { get; set; }   // 1=Lunes, 7=Domingo
    public float Lag1ScrapQty { get; set; }   // Scrap del turno INMEDIATO anterior
    public float Avg3ScrapQty { get; set; }   // Promedio de los últimos 3 turnos
    public float ScrapQty { get; set; }       // Label (Target)
}

public sealed class ModelOutput
{
    [ColumnName("Score")]
    public float PredictedScrapQty { get; set; }
}

// 2. Modelo de datos crudos del ERP
internal sealed class ShiftScrapRow
{
    public string PartNumber { get; set; } = string.Empty;
    public DateTime OperationDate { get; set; }
    public int ShiftId { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public long TicksInicio { get; set; }
    public long TicksFin { get; set; }
    public float ScrapQty { get; set; }
}

