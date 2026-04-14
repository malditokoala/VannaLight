using LLama;
using LLama.Common;
using LLama.Sampling;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Settings;

namespace VannaLight.Infrastructure.AI;

public class LlmClient : ILlmClient, IDisposable
{
    private readonly LLamaWeights _weights;
    private readonly LLamaContext _context;
    private readonly AppSettings _settings;
    private readonly ILogger<LlmClient> _logger;
    private readonly SemaphoreSlim _inferenceLock = new(1, 1);

    public LlmClient(
        AppSettings settings,
        ILlmRuntimeProfileProvider runtimeProfileProvider,
        ILogger<LlmClient> logger)
    {
        _settings = settings;
        _logger = logger;
        var runtimeProfile = runtimeProfileProvider.GetActiveProfile();
        var resolvedContextSize = runtimeProfile.ContextSize.HasValue && runtimeProfile.ContextSize.Value > 0
            ? runtimeProfile.ContextSize.Value
            : (uint)_settings.Llm.ContextSize;
        var resolvedGpuLayers = runtimeProfile.GpuLayerCount.GetValueOrDefault(12);

        var parameters = new ModelParams(_settings.Llm.ModelPath)
        {
            ContextSize = resolvedContextSize,
            GpuLayerCount = resolvedGpuLayers
        };

        _logger.LogInformation(
            "[LlmRuntime] Initializing local model '{ModelPath}' with profile '{ProfileName}' (ContextSize={ContextSize}, GpuLayers={GpuLayers}, Threads={Threads}, BatchSize={BatchSize}, UBatchSize={UBatchSize}).",
            _settings.Llm.ModelPath,
            string.IsNullOrWhiteSpace(runtimeProfile.Name) ? "Default" : runtimeProfile.Name,
            parameters.ContextSize,
            parameters.GpuLayerCount,
            runtimeProfile.Threads,
            runtimeProfile.BatchSize,
            runtimeProfile.UBatchSize);

        _weights = LLamaWeights.LoadFromFile(parameters);
        _context = _weights.CreateContext(parameters);
    }

    public async Task<string> GenerateSqlAsync(string prompt, CancellationToken ct)
    {
        // Wrapper para tu código existente (Data Mode)
        return await ExecuteLlamaInferenceAsync(prompt, ct);
    }

    public async Task<string> CompleteAsync(string prompt, CancellationToken ct)
    {
        // Nuevo método generalista (Docs Mode)
        return await ExecuteLlamaInferenceAsync(prompt, ct);
    }

    // Método centralizado que contiene el motor de LlamaSharp (DRY)
    private async Task<string> ExecuteLlamaInferenceAsync(string prompt, CancellationToken ct)
    {
        await _inferenceLock.WaitAsync(ct);
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var executor = new StatelessExecutor(_weights, _context.Params);

            var inferenceParams = new InferenceParams
            {
                MaxTokens = _settings.Llm.MaxTokens,
                AntiPrompts = new List<string> { "User:", "Pregunta:" },
                // LLamaSharp no es thread-safe con el contexto/pesos compartidos.
                // Serializamos inferencias para evitar crashes nativos intermitentes.
                SamplingPipeline = new DefaultSamplingPipeline
                {
                    Temperature = _settings.Llm.Temperature,
                    TopP = _settings.Llm.TopP,
                    TopK = _settings.Llm.TopK
                }
            };

            var sb = new StringBuilder();

            await foreach (var text in executor.InferAsync(prompt, inferenceParams, cancellationToken: ct))
            {
                sb.Append(text);
            }

            stopwatch.Stop();
            _logger.LogInformation(
                "[LlmPerf] PromptChars={PromptChars} MaxTokens={MaxTokens} OutputChars={OutputChars} TotalMs={TotalMs}",
                prompt?.Length ?? 0,
                inferenceParams.MaxTokens,
                sb.Length,
                stopwatch.ElapsedMilliseconds);

            return sb.ToString().Trim();
        }
        finally
        {
            _inferenceLock.Release();
        }
    }

    public void Dispose()
    {
        _inferenceLock.Dispose();
        _context.Dispose();
        _weights.Dispose();
    }
}
