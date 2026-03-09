using System;
using System.Collections.Generic;
using System.Text;
using VannaLight.Core.Models;

namespace VannaLight.Core.Abstractions;
public interface IPredictionAnswerService
{
    Task<string> HumanizeAsync(PredictionIntent intent, CancellationToken ct);
}