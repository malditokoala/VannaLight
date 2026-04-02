namespace VannaLight.Core.Models;

public sealed record TrainingExample
{
    public long Id { get; init; }
    public string Question { get; init; } = string.Empty;
    public string Sql { get; init; } = string.Empty;
    public string TenantKey { get; init; } = string.Empty;
    public string? Domain { get; init; }
    public string ConnectionName { get; init; } = string.Empty;
    public string? IntentName { get; init; }
    public bool IsVerified { get; init; }
    public int Priority { get; init; }
    public DateTime CreatedUtc { get; init; }
    public DateTime LastUsedUtc { get; init; }
    public long UseCount { get; init; }
}

public sealed record TrainingExampleUpsert(
    string Question,
    string Sql,
    string? TenantKey,
    string? Domain,
    string? ConnectionName,
    string? IntentName,
    bool IsVerified,
    int Priority
);

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
