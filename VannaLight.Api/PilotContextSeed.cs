internal sealed record PilotContextSeed(
    string TenantKey,
    string DisplayName,
    string? Description,
    string Domain,
    string ConnectionName,
    bool IsDefault);
