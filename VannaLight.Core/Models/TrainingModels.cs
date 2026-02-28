namespace VannaLight.Core.Models;

public sealed record TrainingExample
{
    public long Id { get; init; }
    public string Question { get; init; } = string.Empty;
    public string Sql { get; init; } = string.Empty;
    public DateTime CreatedUtc { get; init; }
    public DateTime LastUsedUtc { get; init; }
    public long UseCount { get; init; }
}

public sealed record RetrievedExample(
    TrainingExample Example,
    double Score
);

public sealed record RetrievedSchemaDoc(
    TableSchemaDoc Doc,
    double Score
);

public sealed record RetrievalContext(
    IReadOnlyList<RetrievedExample> Examples,
    IReadOnlyList<RetrievedSchemaDoc> SchemaDocs
);