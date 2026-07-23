@echo off
chcp 65001 >nul
setlocal
rem 一键给业务库补最近缺的列（PinnedUntil / LastActiveAt 等）
rem 用法: fix-pinned-until.bat
rem   或: fix-pinned-until.bat D:\你的路径\data\forum.db

set "DB=%~1"
if "%DB%"=="" (
  if defined BS_DATA_DIR (set "DB=%BS_DATA_DIR%\forum.db") else (set "DB=%~dp0..\data\forum.db")
)

echo 目标库: %DB%
if not exist "%DB%" (
  echo [错误] 找不到库文件
  exit /b 1
)

where sqlite3 >nul 2>&1
if errorlevel 1 (
  echo 未找到 sqlite3，改用 DbSchemaMigrate...
  "%~dp0..\publish\tools\DbSchemaMigrate.exe" --db "%DB%" --nopause
  exit /b %errorlevel%
)

sqlite3 "%DB%" "SELECT COUNT(*) AS threads FROM sqlite_master WHERE type='table' AND name='Threads';"
sqlite3 "%DB%" "ALTER TABLE Threads ADD COLUMN PinnedUntil TEXT NULL;" 2>nul
sqlite3 "%DB%" "ALTER TABLE Users ADD COLUMN LastActiveAt TEXT NULL;" 2>nul
sqlite3 "%DB%" "PRAGMA table_info(Threads);" | findstr /i PinnedUntil
if errorlevel 1 (
  echo [失败] 仍没有 PinnedUntil，请确认上面是业务库且有 Threads 表
  exit /b 2
)
echo [成功] Threads.PinnedUntil 已存在
exit /b 0
