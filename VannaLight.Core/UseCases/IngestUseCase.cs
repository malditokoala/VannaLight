using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;
using System.Text.Json;

namespace VannaLight.Core.UseCases;

public class IngestUseCase(ISchemaIngestor ingestor, ISchemaStore store)
{
    public async Task ExecuteAsync(string sqlServerConnString, string sqlitePath, CancellationToken ct = default)
    {
        // 1. Inicializar la base de datos local si no existe
        await store.InitializeAsync(sqlitePath, ct);

        // 2. Extraer el esquema de SQL Server
        var tables = await ingestor.ReadSchemaAsync(sqlServerConnString, ct);

        // 3. Transformar los modelos de tabla a documentos de esquema (SchemaDocs)
        var docs = new List<TableSchemaDoc>();
        foreach (var table in tables)
        {
            var docText = $"Tabla: {table.Schema}.{table.Name}. " +
                          $"Columnas: {string.Join(", ", table.Columns.Select(c => c.Name))}.";

            var json = JsonSerializer.Serialize(table);

            docs.Add(new TableSchemaDoc(table.Schema, table.Name, docText, json));
        }

        // 4. Guardar en SQLite
        await store.UpsertSchemaDocsAsync(sqlitePath, docs, ct);
    }
}