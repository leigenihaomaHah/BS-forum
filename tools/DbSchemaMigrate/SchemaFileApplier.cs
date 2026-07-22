using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;

namespace DbSchemaMigrate;

public sealed class SchemaEnsureFile
{
    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("tables")]
    public List<TableDef> Tables { get; set; } = [];

    [JsonPropertyName("columns")]
    public List<ColumnDef> Columns { get; set; } = [];

    [JsonPropertyName("indexes")]
    public List<string> Indexes { get; set; } = [];
}

public sealed class TableDef
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("sql")]
    public string Sql { get; set; } = "";
}

public sealed class ColumnDef
{
    [JsonPropertyName("table")]
    public string Table { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("sqlType")]
    public string SqlType { get; set; } = "";
}

public static class SchemaFileApplier
{
    public static async Task<ApplyResult> ApplyAsync(string dbPath, string schemaJsonPath, TextWriter log)
    {
        if (!File.Exists(schemaJsonPath))
            throw new FileNotFoundException("找不到 schema 清单文件", schemaJsonPath);

        var json = await File.ReadAllTextAsync(schemaJsonPath);
        var schema = JsonSerializer.Deserialize<SchemaEnsureFile>(json)
            ?? throw new InvalidOperationException("schema-ensure.json 解析失败");

        var dir = Path.GetDirectoryName(Path.GetFullPath(dbPath));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var result = new ApplyResult();
        await using var conn = new SqliteConnection($"Data Source={dbPath};Cache=Shared;Mode=ReadWriteCreate");
        await conn.OpenAsync();

        await using (var pragma = conn.CreateCommand())
        {
            pragma.CommandText = "PRAGMA busy_timeout=5000; PRAGMA journal_mode=WAL;";
            await pragma.ExecuteNonQueryAsync();
        }

        log.WriteLine($"清单 version={schema.Version}  tables={schema.Tables.Count}  columns={schema.Columns.Count}");

        foreach (var table in schema.Tables)
        {
            if (string.IsNullOrWhiteSpace(table.Name) || string.IsNullOrWhiteSpace(table.Sql))
                continue;

            if (await TableExistsAsync(conn, table.Name))
            {
                result.TablesSkipped++;
                log.WriteLine($"  [跳过表] {table.Name}");
                continue;
            }

            await ExecAsync(conn, table.Sql);
            result.TablesCreated++;
            log.WriteLine($"  [新建表] {table.Name}");
        }

        foreach (var col in schema.Columns)
        {
            if (string.IsNullOrWhiteSpace(col.Table) || string.IsNullOrWhiteSpace(col.Name))
                continue;

            if (!await TableExistsAsync(conn, col.Table))
            {
                result.ColumnsSkipped++;
                log.WriteLine($"  [跳过列] {col.Table}.{col.Name}（表不存在，请先用站点初始化基础库或补 CREATE）");
                continue;
            }

            if (await ColumnExistsAsync(conn, col.Table, col.Name))
            {
                result.ColumnsSkipped++;
                log.WriteLine($"  [跳过列] {col.Table}.{col.Name}");
                continue;
            }

            var sql = $"""ALTER TABLE "{col.Table}" ADD COLUMN "{col.Name}" {col.SqlType};""";
            await ExecAsync(conn, sql);
            result.ColumnsAdded++;
            log.WriteLine($"  [新增列] {col.Table}.{col.Name}");
        }

        foreach (var idx in schema.Indexes)
        {
            if (string.IsNullOrWhiteSpace(idx)) continue;
            try
            {
                await ExecAsync(conn, idx);
                result.IndexesApplied++;
            }
            catch (Exception ex)
            {
                result.IndexWarnings++;
                log.WriteLine($"  [索引警告] {ex.Message}");
            }
        }

        return result;
    }

    static async Task ExecAsync(SqliteConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    static async Task<bool> TableExistsAsync(SqliteConnection conn, string table)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$n LIMIT 1;";
        cmd.Parameters.AddWithValue("$n", table);
        var o = await cmd.ExecuteScalarAsync();
        return o != null && o != DBNull.Value;
    }

    static async Task<bool> ColumnExistsAsync(SqliteConnection conn, string table, string column)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{table}\")";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = reader["name"]?.ToString() ?? "";
            if (string.Equals(name, column, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}

public sealed class ApplyResult
{
    public int TablesCreated { get; set; }
    public int TablesSkipped { get; set; }
    public int ColumnsAdded { get; set; }
    public int ColumnsSkipped { get; set; }
    public int IndexesApplied { get; set; }
    public int IndexWarnings { get; set; }
}
