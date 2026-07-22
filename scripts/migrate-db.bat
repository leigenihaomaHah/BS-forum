@echo off
chcp 65001 >nul
setlocal EnableExtensions EnableDelayedExpansion

rem ============================================================
rem  刷库工具：按 schema-ensure.json 幂等补表/补列
rem  用法:
rem    migrate-db.bat
rem    migrate-db.bat D:\BS\BS\data\forum.db
rem    set BS_DATA_DIR=D:\BS\BS\data && migrate-db.bat
rem ============================================================

set "ROOT=%~dp0.."
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"

set "DB=%~1"
if "%DB%"=="" (
  if defined BS_DATA_DIR (
    set "DB=%BS_DATA_DIR%\forum.db"
  ) else (
    set "DB=%ROOT%\data\forum.db"
  )
)

set "TOOL_OUT=%ROOT%\publish\tools"
set "EXE=%TOOL_OUT%\DbSchemaMigrate.exe"

echo.
echo [刷库] 目标: %DB%
echo.

where dotnet >nul 2>&1
if errorlevel 1 (
  echo [错误] 未找到 dotnet
  exit /b 1
)

echo [1/2] 发布 DbSchemaMigrate...
dotnet publish "%ROOT%\tools\DbSchemaMigrate\DbSchemaMigrate.csproj" -c Release -o "%TOOL_OUT%" --nologo
if errorlevel 1 (
  echo [错误] 刷库工具发布失败
  exit /b 1
)

echo [2/2] 执行刷库...
"%EXE%" --db "%DB%" --nopause
if errorlevel 1 (
  echo [错误] 刷库失败
  exit /b 1
)

echo.
echo 刷库成功。可以继续发布站点 / 重启应用池。
exit /b 0
