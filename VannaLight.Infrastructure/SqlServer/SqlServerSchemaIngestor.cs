using Microsoft.Data.SqlClient;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Infrastructure.SqlServer;

public sealed class SqlServerSchemaIngestor : ISchemaIngestor
{
    public async Task<IReadOnlyList<TableSchema>> ReadSchemaAsync(string sqlServerConnectionString, CancellationToken ct)
    {
        await using var c = new SqlConnection(sqlServerConnectionString);
        await c.OpenAsync(ct);

        var schemas = new List<TableSchema>();

        var objects = new List<(string Schema, string Name, string? Description, string Type)>();
        await using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT 
                    s.name AS SchemaName, 
                    o.name AS ObjectName, 
                    CAST(ep.value AS NVARCHAR(4000)) AS Description,
                    o.type AS ObjectType
                FROM sys.objects o
                JOIN sys.schemas s 
                    ON s.schema_id = o.schema_id
                LEFT JOIN sys.extended_properties ep 
                    ON ep.major_id = o.object_id
                    AND ep.minor_id = 0
                    AND ep.name = 'MS_Description'
                WHERE o.type IN ('U', 'V')
                  AND o.is_ms_shipped = 0
                ORDER BY s.name, o.name;";

            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                objects.Add((
                    r.GetString(0),
                    r.GetString(1),
                    r.IsDBNull(2) ? null : r.GetString(2),
                    r.GetString(3)
                ));
            }
        }

        foreach (var o in objects)
        {
            var cols = await ReadColumnsAsync(c, o.Schema, o.Name, ct);
            var pks = o.Type == "U"
                ? await ReadPrimaryKeysAsync(c, o.Schema, o.Name, ct)
                : new List<string>();

            var fks = o.Type == "U"
                ? await ReadForeignKeysAsync(c, o.Schema, o.Name, ct)
                : new List<ForeignKeyInfo>();

            schemas.Add(new TableSchema(o.Schema, o.Name, o.Description, cols, pks, fks));
        }

        return schemas;
    }

    private async Task<List<ColumnSchema>> ReadColumnsAsync(SqlConnection c, string schema, string table, CancellationToken ct)
    {
        var cols = new List<ColumnSchema>();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            SELECT 
                c.name,
                ty.name AS SqlType,
                c.is_nullable,
                c.max_length,
                c.precision,
                c.scale
            FROM sys.objects o
            JOIN sys.schemas s 
                ON s.schema_id = o.schema_id
            JOIN sys.columns c 
                ON c.object_id = o.object_id
            JOIN sys.types ty 
                ON ty.user_type_id = c.user_type_id
            WHERE s.name = @Schema
              AND o.name = @Table
              AND o.type IN ('U', 'V')
            ORDER BY c.column_id;";
        cmd.Parameters.AddWithValue("@Schema", schema);
        cmd.Parameters.AddWithValue("@Table", table);

        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            cols.Add(new ColumnSchema(
                r.GetString(0),
                r.GetString(1),
                r.GetBoolean(2),
                r.IsDBNull(3) ? null : r.GetInt16(3),
                r.IsDBNull(4) ? null : r.GetByte(4),
                r.IsDBNull(5) ? null : r.GetByte(5)));
        }

        return cols;
    }

    private async Task<List<string>> ReadPrimaryKeysAsync(SqlConnection c, string schema, string table, CancellationToken ct)
    {
        var pks = new List<string>();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            SELECT c.name
            FROM sys.indexes i
            JOIN sys.index_columns ic 
                ON i.object_id = ic.object_id 
               AND i.index_id = ic.index_id
            JOIN sys.columns c 
                ON ic.object_id = c.object_id 
               AND c.column_id = ic.column_id
            JOIN sys.tables t 
                ON i.object_id = t.object_id
            JOIN sys.schemas s 
                ON t.schema_id = s.schema_id
            WHERE i.is_primary_key = 1
              AND s.name = @Schema
              AND t.name = @Table;";
        cmd.Parameters.AddWithValue("@Schema", schema);
        cmd.Parameters.AddWithValue("@Table", table);

        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            pks.Add(r.GetString(0));
        }

        return pks;
    }

    private async Task<List<ForeignKeyInfo>> ReadForeignKeysAsync(SqlConnection c, string schema, string table, CancellationToken ct)
    {
        var fks = new List<ForeignKeyInfo>();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            SELECT 
                fk.name,
                tp.name,
                cp.name,
                tr.name,
                cr.name,
                sr.name
            FROM sys.foreign_keys fk
            JOIN sys.tables tp 
                ON fk.parent_object_id = tp.object_id
            JOIN sys.schemas sp 
                ON tp.schema_id = sp.schema_id
            JOIN sys.foreign_key_columns fkc 
                ON fk.object_id = fkc.constraint_object_id
            JOIN sys.columns cp 
                ON fkc.parent_object_id = cp.object_id 
               AND fkc.parent_column_id = cp.column_id
            JOIN sys.tables tr 
                ON fk.referenced_object_id = tr.object_id
            JOIN sys.schemas sr 
                ON tr.schema_id = sr.schema_id
            JOIN sys.columns cr 
                ON fkc.referenced_object_id = cr.object_id 
               AND fkc.referenced_column_id = cr.column_id
            WHERE sp.name = @Schema
              AND tp.name = @Table;";
        cmd.Parameters.AddWithValue("@Schema", schema);
        cmd.Parameters.AddWithValue("@Table", table);

        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            fks.Add(new ForeignKeyInfo(
                r.GetString(0),
                schema,
                table,
                r.GetString(2),
                r.GetString(5),
                r.GetString(3),
                r.GetString(4)));
        }

        return fks;
    }
}