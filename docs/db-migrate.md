# 数据库：外置库 + 独立刷库 exe

## 原则

1. **业务库不进发布包**（默认 `项目根\data\forum.db`，由 `BS_DATA_DIR` 指定）。
2. **表/字段升级不靠站点启动**，用独立工具 `DbSchemaMigrate.exe`，部署第一步先刷库。
3. 清单文件 [`tools/DbSchemaMigrate/schema-ensure.json`](../tools/DbSchemaMigrate/schema-ensure.json)：已存在的表/列会跳过。

## 部署顺序

```bat
rem 1）刷库（也可由 publish-iis.bat 第 0 步自动执行）
scripts\migrate-db.bat
rem 或指定库路径:
scripts\migrate-db.bat D:\BS\BS\data\forum.db

rem 2）发布站点
publish-iis.bat

rem 3）回收 IIS 应用程序池
```

手动调用已发布的工具：

```bat
publish\tools\DbSchemaMigrate.exe
```

库路径在同目录 **`migrate.config.json`** 里配置：

```json
{
  "dbPath": "D:\\BS\\publish\\data\\forum.db",
  "schemaPath": ""
}
```

改成你线上 `forum.db` 的实际路径即可，双击 exe 就会用这个路径。
## 如何加字段 / 表

1. 改实体与 `AppDbContext`
2. **只改** `tools/DbSchemaMigrate/schema-ensure.json`：
   - 新表 → `tables` 里加 `CREATE TABLE IF NOT EXISTS ...`
   - 新列 → `columns` 里加 `{ "table":"Threads", "name":"CoverUrl", "sqlType":"TEXT NOT NULL DEFAULT ''" }`
3. 部署前跑 `migrate-db.bat`（或跑完整 `publish-iis.bat`）
4. 再启动/回收站点

**禁止**删线上 `forum.db` 来“升级”。

## 与站点启动的关系

- API 启动只做：连库、`EnsureCreated`（空库建基础表）、种子数据。
- **不会**再自动 `ALTER`；缺列时报错时，先跑刷库 exe。

## 线上缺 PendingReview / PinnedUntil 等列时

对**站点正在用的那份**库执行一次（路径必须等于 `BS_DATA_DIR\forum.db`）：

```bat
scripts\migrate-db.bat 你的\data\forum.db路径
```

或：

```bat
publish\tools\DbSchemaMigrate.exe --db "D:\BS\data\forum.db" --nopause
```

没有 SQLite 管理工具也没关系：这个 exe 就是加列工具。若只缺置顶/沉默相关列，可只跑：

```bat
publish\tools\DbSchemaMigrate.exe --db "D:\BS\data\forum.db" --fix-pinned --nopause
```

成功日志应出现例如：

```text
[新增列] Threads.PinnedUntil
```

或 `--fix-pinned` 时：

```text
[新增] Threads.PinnedUntil
```

若出现「缺少论坛基础表 Users/Threads/Posts」或大量「表不存在，跳过列」：
说明刷到的是**空库/错文件**，不是业务库。请核对 exe 打印的「库绝对路径 / 库文件大小 / 库内已有表」。

然后回收应用池即可。
