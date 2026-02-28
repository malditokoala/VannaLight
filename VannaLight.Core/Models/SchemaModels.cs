namespace VannaLight.Core.Models;

public sealed record ColumnSchema(
    string Name,
    string SqlType,
    bool IsNullable,
    int? MaxLength,
    byte? Precision,
    byte? Scale
);

public sealed record ForeignKeyInfo(
    string Name,
    string FromSchema,
    string FromTable,
    string FromColumn,
    string ToSchema,
    string ToTable,
    string ToColumn
);

public sealed record TableSchema(
    string Schema,
    string Name,
    string? Description,
    IReadOnlyList<ColumnSchema> Columns,
    IReadOnlyList<string> PrimaryKeyColumns,
    IReadOnlyList<ForeignKeyInfo> ForeignKeys
);

public sealed record TableSchemaDoc(
    string Schema,
    string Table,
    string DocText,
    string Json // JSON del TableSchema para inspección/heurísticas
);