using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Settings;
using VannaLight.Core.UseCases;
using VannaLight.Infrastructure.AI;
using VannaLight.Infrastructure.Data;
using VannaLight.Infrastructure.Retrieval;
using VannaLight.Infrastructure.Security;
using VannaLight.Infrastructure.SqlServer;
using Spectre.Console;
using Dapper;
using Microsoft.Data.SqlClient;

// 1. Configuración de Rutas (Ajusta la ruta de tu modelo)
string modelPath = @"C:\Modelos\qwen2.5-coder-7b-instruct-q4_k_m.gguf";
string sqlitePath = "vanna_memory.db";
string sqlServerConnString = "Server=localhost,1433;Database=Northwind;User Id=sa;Password=Chopsuey00;TrustServerCertificate=True;";

// 2. Configurar Inyección de Dependencias
var services = new ServiceCollection();
var settings = AppSettingsFactory.Create(RuntimeProfile.ALTO, modelPath);
services.AddSingleton(settings);

services.AddSingleton<ISchemaIngestor, SqlServerSchemaIngestor>();
services.AddSingleton<ISchemaStore, SqliteSchemaStore>();
services.AddSingleton<ITrainingStore, SqliteTrainingStore>();
services.AddSingleton<IReviewStore, SqliteReviewStore>(); // <-- Registrado aquí
services.AddSingleton<IRetriever, LocalRetriever>();
services.AddSingleton<ISqlValidator, StaticSqlValidator>();
services.AddSingleton<ISqlDryRunner, SqlServerDryRunner>();
services.AddSingleton<ILlmClient, LlmClient>();

services.AddTransient<IngestUseCase>();
services.AddTransient<AskUseCase>();

var serviceProvider = services.BuildServiceProvider();

// 3. Configurar la Interfaz de Línea de Comandos (CLI)
var rootCommand = new RootCommand("Vanna Light Standalone - Asistente Text-to-SQL Local");

// Comando Ingest
var ingestCommand = new Command("ingest", "Lee el esquema de SQL Server y lo guarda en la memoria local.");
ingestCommand.SetHandler(async () =>
{
    Console.WriteLine("Iniciando ingesta de esquema...");
    var useCase = serviceProvider.GetRequiredService<IngestUseCase>();
    await useCase.ExecuteAsync(sqlServerConnString, sqlitePath);
    Console.WriteLine("Ingesta completada exitosamente.");
});

// Comando Ask
var askCommand = new Command("ask", "Genera una consulta T-SQL a partir de lenguaje natural.");
var questionArgument = new Argument<string>("question", "La pregunta en lenguaje natural.");
askCommand.AddArgument(questionArgument);
askCommand.SetHandler(async (string question) =>
{
    Console.WriteLine($"Analizando: '{question}'...");
    var useCase = serviceProvider.GetRequiredService<AskUseCase>();
    var result = await useCase.ExecuteAsync(question, sqlitePath, sqlServerConnString);

    if (result.Success)
    {
        // Usamos Spectre.Console para darle color y formato
        AnsiConsole.MarkupLine("\n[green]--- T-SQL GENERADO ---[/]");
        Console.WriteLine(result.Sql);

        if (result.PassedDryRun)
        {
            AnsiConsole.MarkupLine("\n[cyan][[✓]] Compilación validada (Dry-Run exitoso).[/]");

            // Medida de seguridad: Preguntamos antes de disparar a la BD
            var execute = AnsiConsole.Confirm("¿Deseas ejecutar esta consulta en tu base de datos y ver los resultados?");
            if (execute)
            {
                try
                {
                    using var connection = new SqlConnection(sqlServerConnString);
                    // Usamos dynamic porque no sabemos qué columnas va a devolver el LLM
                    var rows = (await connection.QueryAsync<dynamic>(result.Sql)).ToList();

                    if (rows.Any())
                    {
                        var table = new Table();
                        table.Border(TableBorder.Rounded);

                        // 1. Extraemos los nombres de las columnas de la primera fila
                        var firstRow = (IDictionary<string, object>)rows.First();
                        foreach (var col in firstRow.Keys)
                        {
                            table.AddColumn($"[yellow]{col}[/]");
                        }

                        // 2. Llenamos las filas con los datos
                        foreach (var row in rows)
                        {
                            var dict = (IDictionary<string, object>)row;
                            var values = dict.Values.Select(v => v?.ToString() ?? "[grey]NULL[/]").ToArray();
                            table.AddRow(values);
                        }

                        // 3. ¡Dibujamos la tabla!
                        AnsiConsole.Write(table);
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[yellow]La consulta se ejecutó correctamente pero no devolvió filas.[/]");
                    }
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Error al ejecutar los datos: {ex.Message}[/]");
                }
            }
        }
        else
        {
            AnsiConsole.MarkupLine("\n[yellow][[!]] Dry-Run desactivado o fallido. Riesgo al ejecutar.[/]");
        }
    }
    else
    {
        AnsiConsole.MarkupLine("\n[red]--- ERROR ---[/]");
        Console.WriteLine(result.Error);
        AnsiConsole.MarkupLine("[red]SQL Generado (con fallos):[/]");
        Console.WriteLine(result.Sql);
    }
}, questionArgument);

// --- COMANDOS DE REVISIÓN (ReviewQueue) ---
var reviewCommand = new Command("review", "Gestiona la cola de consultas fallidas o incorrectas.");

// Review List
var reviewListCommand = new Command("list", "Muestra todas las consultas pendientes de revisión.");
reviewListCommand.SetHandler(async () =>
{
    var store = serviceProvider.GetRequiredService<IReviewStore>();
    var pending = await store.GetPendingReviewsAsync(sqlitePath, default);
    Console.WriteLine($"\n--- COLA DE REVISIÓN ({pending.Count} pendientes) ---");
    foreach (var item in pending)
    {
        Console.WriteLine($"[#{item.Id}] Motivo: {item.Reason} | Pregunta: {item.Question}");
    }
});

// Review Approve
var approveIdArg = new Argument<long>("id", "El ID de la revisión a aprobar.");
var reviewApproveCommand = new Command("approve", "Aprueba una consulta y la mueve a los ejemplos de entrenamiento.");
reviewApproveCommand.AddArgument(approveIdArg);
reviewApproveCommand.SetHandler(async (long id) =>
{
    var rStore = serviceProvider.GetRequiredService<IReviewStore>();
    var tStore = serviceProvider.GetRequiredService<ITrainingStore>();

    var item = await rStore.GetReviewByIdAsync(sqlitePath, id, default);
    if (item == null || item.Status != "Pending")
    {
        Console.WriteLine($"Revisión #{id} no encontrada o ya procesada.");
        return;
    }

    await tStore.InsertTrainingExampleAsync(sqlitePath, item.Question, item.GeneratedSql, default);
    await rStore.UpdateReviewStatusAsync(sqlitePath, id, "Approved", default);

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"[✓] Revisión #{id} aprobada. El modelo ahora aprenderá de este ejemplo.");
    Console.ResetColor();
}, approveIdArg);

// Review Fix
var fixIdArg = new Argument<long>("id", "El ID de la revisión a corregir.");
var fixSqlOpt = new Option<string>("--sql", "El código T-SQL corregido.") { IsRequired = true };
var reviewFixCommand = new Command("fix", "Corrige una consulta fallida y la aprueba automáticamente.");
reviewFixCommand.AddArgument(fixIdArg);
reviewFixCommand.AddOption(fixSqlOpt);
reviewFixCommand.SetHandler(async (long id, string sql) =>
{
    var rStore = serviceProvider.GetRequiredService<IReviewStore>();
    var tStore = serviceProvider.GetRequiredService<ITrainingStore>();

    var item = await rStore.GetReviewByIdAsync(sqlitePath, id, default);
    if (item == null || item.Status != "Pending")
    {
        Console.WriteLine($"Revisión #{id} no encontrada o ya procesada.");
        return;
    }

    await tStore.InsertTrainingExampleAsync(sqlitePath, item.Question, sql, default);
    await rStore.UpdateReviewStatusAsync(sqlitePath, id, "Approved", default);

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"[✓] Revisión #{id} corregida y guardada en memoria. Excelente trabajo.");
    Console.ResetColor();
}, fixIdArg, fixSqlOpt);

reviewCommand.AddCommand(reviewListCommand);
reviewCommand.AddCommand(reviewApproveCommand);
reviewCommand.AddCommand(reviewFixCommand);

// Ensamblar comandos al Root
rootCommand.AddCommand(ingestCommand);
rootCommand.AddCommand(askCommand);
rootCommand.AddCommand(reviewCommand);

// 4. Inicializar bases de datos (Aquí se crean las tablas si no existen)
await serviceProvider.GetRequiredService<ISchemaStore>().InitializeAsync(sqlitePath, default);
await serviceProvider.GetRequiredService<ITrainingStore>().InitializeAsync(sqlitePath, default);
await serviceProvider.GetRequiredService<IReviewStore>().InitializeAsync(sqlitePath, default);

// 5. Ejecutar la CLI
await rootCommand.InvokeAsync(args);