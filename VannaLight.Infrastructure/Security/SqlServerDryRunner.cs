using Microsoft.Data.SqlClient;
using VannaLight.Core.Abstractions;

namespace VannaLight.Infrastructure.Security;

public class SqlServerDryRunner : ISqlDryRunner
{
    public async Task<(bool Ok, string? Error)> DryRunAsync(string sqlServerConnectionString, string sql, CancellationToken ct)
    {
        try
        {
            await using var connection = new SqlConnection(sqlServerConnectionString);
            await connection.OpenAsync(ct);

            await using var command = connection.CreateCommand();
            // SET NOEXEC ON le dice a SQL Server que compile la consulta, valide la sintaxis 
            // y los nombres de objetos, pero que NO la ejecute.
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