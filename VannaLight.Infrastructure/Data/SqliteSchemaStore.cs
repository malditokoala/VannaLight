using Dapper;
using Microsoft.Data.Sqlite;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Infrastructure.Data;

public class SqliteSchemaStore : ISchemaStore
{
    public async Task InitializeAsync(string sqlitePath, CancellationToken ct)
    {
        using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        await connection.OpenAsync(ct);

        var createTableSql = @"
            CREATE TABLE IF NOT EXISTS SchemaDocs (
                SchemaName TEXT NOT NULL,
                TableName TEXT NOT NULL,
                DocText TEXT NOT NULL,
                JsonDefinition TEXT NOT NULL,
                PRIMARY KEY (SchemaName, TableName)
            );";

        await connection.ExecuteAsync(createTableSql);
    }

    public async Task UpsertSchemaDocsAsync(string sqlitePath, IReadOnlyList<TableSchemaDoc> docs, CancellationToken ct)
    {
        using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        await connection.OpenAsync(ct);
        using var transaction = connection.BeginTransaction();

        var sql = @"
            INSERT INTO SchemaDocs (SchemaName, TableName, DocText, JsonDefinition) 
            VALUES (@Schema, @Table, @DocText, @Json)
            ON CONFLICT(SchemaName, TableName) DO UPDATE SET 
                DocText = excluded.DocText, 
                JsonDefinition = excluded.JsonDefinition;";

        await connection.ExecuteAsync(sql, docs, transaction: transaction);
        transaction.Commit();
    }

    public async Task<IReadOnlyList<TableSchemaDoc>> GetAllSchemaDocsAsync(string sqlitePath, CancellationToken ct)
    {
        using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        // Colocamos corchetes a [Schema], [Table] y [Json] para evitar conflictos con palabras reservadas
        var result = await connection.QueryAsync<TableSchemaDoc>(
            "SELECT SchemaName as [Schema], TableName as [Table], DocText, JsonDefinition as [Json] FROM SchemaDocs");

        return result.ToList();
    }
}