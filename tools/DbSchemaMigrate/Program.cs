using System.Text.Json;
using DbSchemaMigrate;

static string? GetArg(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    }
    return null;
}

static bool HasFlag(string[] args, string name) =>
    args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

var pauseAtEnd = !HasFlag(args, "--nopause");

int Exit(int code)
{
    if (pauseAtEnd)
    {
        Console.WriteLine();
        Console.WriteLine("按任意键关闭…");
        try { Console.ReadKey(true); } catch { }
    }
    return code;
}

var baseDir = AppContext.BaseDirectory;
var configPath = Path.Combine(baseDir, "migrate.config.json");

string? configDb = null;
string? configSchema = null;
if (File.Exists(configPath))
{
    try
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        var root = doc.RootElement;
        if (root.TryGetProperty("dbPath", out var dbEl))
            configDb = dbEl.GetString();
        if (root.TryGetProperty("schemaPath", out var schemaEl))
            configSchema = schemaEl.GetString();
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("读取 migrate.config.json 失败: " + ex.Message);
        return Exit(1);
    }
}

if (HasFlag(args, "-h") || HasFlag(args, "--help"))
{
    Console.WriteLine("""
        DbSchemaMigrate — 按 schema-ensure.json 补表/补列

        配置文件（与 exe 同目录）: migrate.config.json
          {
            "dbPath": "D:\\BS\\publish\\data\\forum.db",
            "schemaPath": ""
          }

        也可临时覆盖:
          DbSchemaMigrate.exe --db <路径> [--nopause]
        """);
    return Exit(0);
}

var schemaPath = GetArg(args, "--schema");
if (string.IsNullOrWhiteSpace(schemaPath))
    schemaPath = string.IsNullOrWhiteSpace(configSchema) ? null : configSchema;
if (string.IsNullOrWhiteSpace(schemaPath))
    schemaPath = Path.Combine(baseDir, "schema-ensure.json");

var dbPath = GetArg(args, "--db");
if (string.IsNullOrWhiteSpace(dbPath))
    dbPath = configDb;
if (string.IsNullOrWhiteSpace(dbPath))
{
    var dataDir = Environment.GetEnvironmentVariable("BS_DATA_DIR");
    if (!string.IsNullOrWhiteSpace(dataDir))
        dbPath = Path.Combine(dataDir.Trim(), "forum.db");
}

if (string.IsNullOrWhiteSpace(dbPath))
{
    Console.Error.WriteLine("错误: 未配置数据库路径。");
    Console.Error.WriteLine($"请编辑: {configPath}");
    Console.Error.WriteLine("把 dbPath 改成你的 forum.db 完整路径，例如:");
    Console.Error.WriteLine("  \"dbPath\": \"D:\\\\BS\\\\publish\\\\data\\\\forum.db\"");
    return Exit(1);
}

dbPath = Path.GetFullPath(dbPath);
schemaPath = Path.IsPathRooted(schemaPath)
    ? Path.GetFullPath(schemaPath)
    : Path.GetFullPath(Path.Combine(baseDir, schemaPath));

Console.WriteLine("========================================");
Console.WriteLine("  BS Forum 数据库结构刷库工具");
Console.WriteLine("========================================");
Console.WriteLine($"配置文件: {configPath}");
Console.WriteLine($"库文件:   {dbPath}");
Console.WriteLine($"清单文件: {schemaPath}");
Console.WriteLine($"库存在:   {(File.Exists(dbPath) ? "是" : "否（将创建）")}");
Console.WriteLine();

if (!File.Exists(schemaPath))
{
    Console.Error.WriteLine("错误: 找不到 schema-ensure.json");
    return Exit(1);
}

try
{
    var result = await SchemaFileApplier.ApplyAsync(dbPath, schemaPath, Console.Out);
    Console.WriteLine();
    Console.WriteLine("完成:");
    Console.WriteLine($"  新建表 {result.TablesCreated}，跳过表 {result.TablesSkipped}");
    Console.WriteLine($"  新增列 {result.ColumnsAdded}，跳过列 {result.ColumnsSkipped}");
    Console.WriteLine($"  索引语句 {result.IndexesApplied}（警告 {result.IndexWarnings}）");
    return Exit(0);
}
catch (Exception ex)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("失败: " + ex.Message);
    return Exit(2);
}
