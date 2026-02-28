using LLama;
using LLama.Common;
using LLama.Sampling; // <-- Añadimos este using
using System.Text;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Settings;

namespace VannaLight.Infrastructure.AI;

public class LlmClient : ILlmClient, IDisposable
{
    private readonly LLamaWeights _weights;
    private readonly LLamaContext _context;
    private readonly AppSettings _settings;

    public LlmClient(AppSettings settings)
    {
        _settings = settings;
        var parameters = new ModelParams(_settings.Llm.ModelPath)
        {
            ContextSize = (uint)_settings.Llm.ContextSize,
            GpuLayerCount = 35 // Ajustado para tu RTX 4060
        };

        _weights = LLamaWeights.LoadFromFile(parameters);
        _context = _weights.CreateContext(parameters);
    }

    public async Task<string> GenerateSqlAsync(string prompt, CancellationToken ct)
    {
        var executor = new StatelessExecutor(_weights, _context.Params);

        var inferenceParams = new InferenceParams
        {
            MaxTokens = _settings.Llm.MaxTokens,
            AntiPrompts = new List<string> { "User:", "Pregunta:" },
            // Los parámetros de temperatura y "Top" ahora van dentro del Pipeline de Muestreo:
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

        return sb.ToString();
    }

    public void Dispose()
    {
        _context.Dispose();
        _weights.Dispose();
    }
}