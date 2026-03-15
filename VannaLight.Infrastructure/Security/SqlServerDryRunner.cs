using Microsoft.Data.SqlClient;
using VannaLight.Core.Abstractions;

namespace VannaLight.Infrastructure.Security;

public class SqlServerDryRunner : ISqlDryRunner
{
    private const int DryRunTimeoutSeconds = 5;

    public async Task<(bool Ok, string? Error)> DryRunAsync(
        string sqlServerConnectionString,
        string sql,
        CancellationToken ct)
    {
        try
        {
            await using var connection = new SqlConnection(sqlServerConnectionString);
            await connection.OpenAsync(ct);

            await using var command = connection.CreateCommand();
            command.CommandTimeout = DryRunTimeoutSeconds;

            // Compila, valida sintaxis y objetos, pero no ejecuta
            command.CommandText = $"SET NOEXEC ON;\n{sql}\nSET NOEXEC OFF;";

            await command.ExecuteNonQueryAsync(ct);
            return (true, null);
        }
        catch (SqlException ex)
        {
            return (false, ex.Message);
        }
    }
}
