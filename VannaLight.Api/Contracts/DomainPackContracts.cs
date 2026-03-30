namespace VannaLight.Api.Contracts;

public sealed record DomainPackDto(
    string Version,
    string ExportedUtc,
    string TenantKey,
    string TenantDisplayName,
    string? TenantDescription,
    string Domain,
    string ConnectionName,
    string? SystemProfileKey,
    IReadOnlyList<DomainPackSystemConfigEntryDto> SystemConfigEntries,
    IReadOnlyList<DomainPackAllowedObjectDto> AllowedObjects,
    IReadOnlyList<DomainPackBusinessRuleDto> BusinessRules,
    IReadOnlyList<DomainPackSemanticHintDto> SemanticHints,
    IReadOnlyList<DomainPackQueryPatternDto> QueryPatterns,
    IReadOnlyList<DomainPackTrainingExampleDto> TrainingExamples);

public sealed record DomainPackSystemConfigEntryDto(
    string Section,
    string Key,
    string? Value,
    string ValueType,
    bool IsEditableInUi,
    string? ValidationRule,
    string? Description);

public sealed record DomainPackAllowedObjectDto(
    string SchemaName,
    string ObjectName,
    string ObjectType,
    bool IsActive,
    string? Notes);

public sealed record DomainPackBusinessRuleDto(
    string RuleKey,
    string RuleText,
    int Priority,
    bool IsActive);

public sealed record DomainPackSemanticHintDto(
    string HintKey,
    string HintType,
    string? DisplayName,
    string? ObjectName,
    string? ColumnName,
    string HintText,
    int Priority,
    bool IsActive);

public sealed record DomainPackQueryPatternDto(
    string PatternKey,
    string IntentName,
    string? Description,
    string SqlTemplate,
    int? DefaultTopN,
    string? MetricKey,
    string? DimensionKey,
    string? DefaultTimeScopeKey,
    int Priority,
    bool IsActive,
    IReadOnlyList<DomainPackQueryPatternTermDto> Terms);

public sealed record DomainPackQueryPatternTermDto(
    string Term,
    string TermGroup,
    string MatchMode,
    bool IsRequired,
    bool IsActive);

public sealed record DomainPackTrainingExampleDto(
    string Question,
    string Sql,
    string? IntentName,
    bool IsVerified,
    int Priority);
