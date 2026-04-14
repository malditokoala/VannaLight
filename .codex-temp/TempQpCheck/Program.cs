using System;
using Microsoft.Data.Sqlite;
var db = Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\VannaLight\Data\vanna_memory.db");
using var conn = new SqliteConnection($"Data Source={db}");
conn.Open();
using var cmd = conn.CreateCommand();
cmd.CommandText = "select count(*) from QueryPatterns where Domain='erp-kpi-pilot' and IsActive=1;";
Console.WriteLine(cmd.ExecuteScalar());
