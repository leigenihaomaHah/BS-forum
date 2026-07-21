@echo off
chcp 65001 >nul
setlocal EnableExtensions
rem ============================================================
rem  修复 SQLite「readonly database」——改的是 ACL，不是只读勾选
rem  用法（管理员 CMD）:
rem    fix-readonly.bat
rem    fix-readonly.bat "D:\站点物理路径" "应用程序池名"
rem ============================================================

set "DIR=%~1"
set "POOL=%~2"
if "%DIR%"=="" set "DIR=%~dp0publish\iis"
if "%POOL%"=="" set "POOL=BS"

if "%DIR:~-1%"=="\" set "DIR=%DIR:~0,-1%"
if not exist "%DIR%" (
  echo [错误] 目录不存在: %DIR%
  exit /b 1
)

if not exist "%DIR%\App_Data" mkdir "%DIR%\App_Data"
if not exist "%DIR%\logs" mkdir "%DIR%\logs"

echo.
echo 目标目录: %DIR%
echo 应用程序池: %POOL%
echo.

echo [1/3] 清除 forum.db* 只读属性...
attrib -R "%DIR%\App_Data\*.*" /S >nul 2>&1
attrib -R "%DIR%\App_Data\forum.db" >nul 2>&1
attrib -R "%DIR%\App_Data\forum.db-wal" >nul 2>&1
attrib -R "%DIR%\App_Data\forum.db-shm" >nul 2>&1

echo [2/3] 授予 IIS 应用程序池「修改」权限...
icacls "%DIR%\App_Data" /grant "IIS AppPool\%POOL%:(OI)(CI)M" /T
icacls "%DIR%\logs" /grant "IIS AppPool\%POOL%:(OI)(CI)M" /T
icacls "%DIR%" /grant "IIS AppPool\%POOL%:(OI)(CI)M"
icacls "%DIR%\App_Data" /grant "IIS_IUSRS:(OI)(CI)M" /T
icacls "%DIR%\logs" /grant "IIS_IUSRS:(OI)(CI)M" /T

echo [3/3] 写探针测试...
echo ok>"%DIR%\App_Data\.write_probe" 2>nul
if errorlevel 1 (
  echo [失败] 当前用户都写不进 App_Data，请用管理员身份运行本脚本
  exit /b 1
)
del /f /q "%DIR%\App_Data\.write_probe" >nul 2>&1

echo.
echo 尝试回收应用程序池...
%windir%\system32\inetsrv\appcmd.exe recycle apppool /apppool.name:"%POOL%" 2>nul
if errorlevel 1 (
  echo （未找到 appcmd 或池名不对，请在 IIS 里手动回收应用程序池）
)

echo.
echo ========================================
echo  完成。请再试「新增分类」。
echo  若仍报 readonly：
echo   1. 确认 IIS 站点物理路径就是: %DIR%
echo   2. 确认应用程序池名就是: %POOL%
echo   3. 应用程序池标识若是自定义账号，把该账号也加入 App_Data 的「修改」权限
echo ========================================
exit /b 0
