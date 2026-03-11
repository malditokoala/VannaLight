using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VannaLight.Core.Models;

namespace VannaLight.Core.Abstractions;

public interface ILlmProfileStore
{
    Task<IEnumerable<LlmRuntimeProfile>> GetAllAsync(CancellationToken ct = default);

    Task<bool> ActivateAsync(int id, CancellationToken ct = default);

    Task<bool> UpdateAsync(int id, int? gpuLayers, uint? contextSize, int? batchSize, int? uBatchSize, int? threads, CancellationToken ct = default);
}