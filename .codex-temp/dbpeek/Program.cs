using Microsoft.Data.Sqlite;
SQLitePCL.Batteries_V2.Init();
var db = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VannaLight", "Data", "vanna_memory.db");
using var cn = new SqliteConnection($"Data Source={db}");
cn.Open();
using (var cmd = cn.CreateCommand())
{
    cmd.CommandText = @"
UPDATE Tenants SET IsActive = 1, UpdatedUtc = @now WHERE TenantKey IN ('zenit-mx', 'northwind-demo');
UPDATE TenantDomains
SET IsActive = 1,
    UpdatedUtc = @now
WHERE Id IN (
    SELECT td.Id
    FROM TenantDomains td
    INNER JOIN Tenants t ON t.Id = td.TenantId
    INNER JOIN ConnectionProfiles cp ON cp.ConnectionName = td.ConnectionName
    WHERE t.TenantKey IN ('zenit-mx', 'northwind-demo')
      AND cp.IsActive = 1
);
";
    cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
    cmd.ExecuteNonQuery();
}
using (var cmd = cn.CreateCommand())
{
    cmd.CommandText = @"SELECT t.TenantKey, t.IsActive, td.Domain, td.ConnectionName, td.IsActive
FROM Tenants t
LEFT JOIN TenantDomains td ON td.TenantId = t.Id
WHERE t.TenantKey IN ('zenit-mx', 'northwind-demo')
ORDER BY t.TenantKey, td.Domain;";
    using var r = cmd.ExecuteReader();
    while (r.Read())
    {
        Console.WriteLine($"{r.GetString(0)} | tenantActive={r.GetBoolean(1)} | domain={(r.IsDBNull(2)?"":r.GetString(2))} | connection={(r.IsDBNull(3)?"":r.GetString(3))} | mappingActive={(r.IsDBNull(4)?"":r.GetBoolean(4).ToString())}");
    }
}
