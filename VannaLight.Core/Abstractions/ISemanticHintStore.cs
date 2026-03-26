using VannaLight.Core.Models;

namespace VannaLight.Core.Abstractions;

public interface ISemanticHintStore
{
    Task<IReadOnlyList<SemanticHint>> GetActiveHintsAsync(
        string sqlitePath,
        string domain,
        int maxHints,
        CancellationToken ct = default);

    Task<IReadOnlyList<SemanticHint>> GetAllHintsAsync(
        string sqlitePath,
        string domain,
        CancellationToken ct = default);

    Task<long> UpsertAsync(
        string sqlitePath,
        SemanticHint hint,
        CancellationToken ct = default);

    Task<bool> SetIsActiveAsync(
        string sqlitePath,
        long id,
        bool isActive,
        CancellationToken ct = default);
}
