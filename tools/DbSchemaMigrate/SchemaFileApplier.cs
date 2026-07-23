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
    /// <summary>只补付费置顶/沉默用户相关列，不跑整份清单。</summary>
    public static async Task<(bool Ok, string? Error)> FixPinnedColumnsAsync(string dbPath, TextWriter log)
    {
        dbPath = Path.GetFullPath(dbPath);
        if (!File.Exists(dbPath))
            return (false, "库文件不存在: " + dbPath);

        await using var conn = new SqliteConnection($"Data Source={dbPath};Cache=Shared;Mode=ReadWrite");
        await conn.OpenAsync();
        await using (var pragma = conn.CreateCommand())
        {
            pragma.CommandText = "PRAGMA busy_timeout=5000;";
            await pragma.ExecuteNonQueryAsync();
        }

        if (!await TableExistsAsync(conn, "Threads"))
            return (false, "没有 Threads 表，这不是业务库: " + dbPath);
        if (!await TableExistsAsync(conn, "Users"))
            return (false, "没有 Users 表，这不是业务库: " + dbPath);

        log.WriteLine($"库: {dbPath}");
        await EnsureColumnAsync(conn, log, "Threads", "PinnedUntil", "TEXT NULL");
        await EnsureColumnAsync(conn, log, "Users", "LastActiveAt", "TEXT NULL");
        return (true, null);
    }

    static async Task EnsureColumnAsync(SqliteConnection conn, TextWriter log, string table, string column, string sqlType)
    {
        if (await ColumnExistsAsync(conn, table, column))
        {
            log.WriteLine($"  [已有] {table}.{column}");
            return;
        }
        await ExecAsync(conn, $"""ALTER TABLE "{table}" ADD COLUMN "{column}" {sqlType};""");
        log.WriteLine($"  [新增] {table}.{column}");
    }

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

        var fi = new FileInfo(dbPath);
        log.WriteLine($"库绝对路径: {Path.GetFullPath(dbPath)}");
        log.WriteLine($"库文件大小: {(fi.Exists ? $"{fi.Length} 字节，修改时间 {fi.LastWriteTime:yyyy-MM-dd HH:mm:ss}" : "（尚不存在，即将创建空库）")}");

        var existingTables = await ListTablesAsync(conn);
        log.WriteLine($"库内已有表 ({existingTables.Count}): {string.Join(", ", existingTables.OrderBy(x => x).Take(40))}{(existingTables.Count > 40 ? " ..." : "")}");

        // 核心表缺失时，补列会全部跳过——直接失败，避免误以为刷库成功
        string[] coreTables = ["Users", "Threads", "Posts", "Forums", "Categories"];
        var missingCore = coreTables.Where(t => !existingTables.Contains(t)).ToList();
        var needsCoreColumns = schema.Columns.Any(c =>
            coreTables.Contains(c.Table, StringComparer.OrdinalIgnoreCase));
        if (missingCore.Count > 0 && needsCoreColumns)
        {
            log.WriteLine();
            log.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
            log.WriteLine("错误: 当前库缺少论坛基础表: " + string.Join(", ", missingCore));
            log.WriteLine("这说明你刷到的不是已初始化的业务库（或是空库）。");
            log.WriteLine("补列（如 Threads.PinnedUntil）无法执行。");
            log.WriteLine();
            log.WriteLine("请核对:");
            log.WriteLine("  1) migrate.config.json / --db 是否等于 IIS 的 BS_DATA_DIR\\forum.db");
            log.WriteLine("  2) 不要刷 publish 下新生成的空库");
            log.WriteLine("  3) 真库应已有 Users/Threads/Posts（站点曾成功跑过）");
            log.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
            throw new InvalidOperationException(
                "目标库缺少基础表 " + string.Join("/", missingCore) + "，已中止，避免误刷空库。");
        }

        var pinnedInSchema = schema.Columns.Any(c =>
            string.Equals(c.Table, "Threads", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(c.Name, "PinnedUntil", StringComparison.OrdinalIgnoreCase));
        log.WriteLine($"清单含 Threads.PinnedUntil: {(pinnedInSchema ? "是" : "否（请更新 schema-ensure.json 后重新发布 tools）")}");
        log.WriteLine();

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

    static async Task<HashSet<string>> ListTablesAsync(SqliteConnection conn)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = reader.GetString(0);
            if (!string.IsNullOrEmpty(name))
                set.Add(name);
        }
        return set;
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
