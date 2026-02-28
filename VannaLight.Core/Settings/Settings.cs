namespace VannaLight.Core.Settings;

public enum RuntimeProfile { MEDIO, ALTO }

public sealed record LlmSettings(
    string ModelPath,
    int ContextSize,
    float Temperature,
    float TopP,
    int TopK,
    int MaxTokens,
    int Threads
);

public sealed record RetrievalSettings(
    int TopExamples,
    double MinExampleScore, // si no llega a este umbral, se considera "sin match" fuerte
    int TopSchemaDocs
);

public sealed record SecuritySettings(
    bool DryRunEnabledByDefault
);

public sealed record AppSettings(
    RuntimeProfile Profile,
    LlmSettings Llm,
    RetrievalSettings Retrieval,
    SecuritySettings Security
);

public static class AppSettingsFactory
{
    public static AppSettings Create(RuntimeProfile profile, string modelPath)
    {
        // Valores conservadores y "offline-friendly"
        return profile switch
        {
            RuntimeProfile.MEDIO => new AppSettings(
                profile,
                new LlmSettings(
                    ModelPath: modelPath,
                    ContextSize: 2048,
                    Temperature: 0.1f,
                    TopP: 0.9f,
                    TopK: 40,
                    MaxTokens: 512,
                    Threads: Math.Max(2, Environment.ProcessorCount - 2)
                ),
                new RetrievalSettings(
                    TopExamples: 3,
                    MinExampleScore: 2.5, // BM25 depende de corpus; ajustable
                    TopSchemaDocs: 5
                ),
                new SecuritySettings(
                    DryRunEnabledByDefault: true
                )
            ),
            RuntimeProfile.ALTO => new AppSettings(
                profile,
                new LlmSettings(
                    ModelPath: modelPath,
                    ContextSize: 4096,
                    Temperature: 0.1f,
                    TopP: 0.9f,
                    TopK: 80,
                    MaxTokens: 768,
                    Threads: Math.Max(4, Environment.ProcessorCount - 1)
                ),
                new RetrievalSettings(
                    TopExamples: 5,
                    MinExampleScore: 2.0,
                    TopSchemaDocs: 10
                ),
                new SecuritySettings(
                    DryRunEnabledByDefault: true
                )
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(profile))
        };
    }
}