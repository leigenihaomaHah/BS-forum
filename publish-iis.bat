@echo off
chcp 65001 >nul
setlocal EnableExtensions EnableDelayedExpansion

rem ============================================================
rem  BS Forum 一键发布到 IIS
rem  用法:
rem    publish-iis.bat
rem    publish-iis.bat D:\inetpub\wwwroot\BSForum
rem
rem  业务库默认写到站点外:  ^<项目根^>\data\forum.db
rem  可用环境变量 BS_DATA_DIR 覆盖。发布包内不含 forum.db。
rem ============================================================

set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"

set "OUT=%ROOT%\publish\iis"
if not "%~1"=="" set "OUT=%~1"

if defined BS_DATA_DIR (
  set "DATA_DIR=%BS_DATA_DIR%"
) else (
  set "DATA_DIR=%ROOT%\data"
)

echo.
echo ========================================
echo   BS Forum IIS 一键发布
echo ========================================
echo 项目目录: %ROOT%
echo 输出目录: %OUT%
echo 数据目录: %DATA_DIR%  ^(不在发布包内^)
echo.

where dotnet >nul 2>&1
if errorlevel 1 (
  echo [错误] 未找到 dotnet，请先安装 .NET 8 SDK
  echo https://dotnet.microsoft.com/download/dotnet/8.0
  exit /b 1
)

where npm >nul 2>&1
if errorlevel 1 (
  echo [错误] 未找到 npm，请先安装 Node.js LTS
  echo https://nodejs.org/
  exit /b 1
)

if not exist "%DATA_DIR%\" mkdir "%DATA_DIR%" >nul 2>&1

echo [0/5] 刷库（表/字段，已存在则跳过）...
dotnet publish "%ROOT%\tools\DbSchemaMigrate\DbSchemaMigrate.csproj" -c Release -o "%ROOT%\publish\tools" --nologo
if errorlevel 1 (
  echo [错误] DbSchemaMigrate 发布失败
  exit /b 1
)
"%ROOT%\publish\tools\DbSchemaMigrate.exe" --db "%DATA_DIR%\forum.db" --nopause
if errorlevel 1 (
  echo [错误] 刷库失败，已中止发布
  exit /b 1
)

echo [1/5] 检查前端依赖...
cd /d "%ROOT%\frontend"
if not exist "node_modules\" (
  echo       正在 npm install ...
  call npm install
  if errorlevel 1 (
    echo [错误] npm install 失败
    exit /b 1
  )
) else (
  echo       node_modules 已存在，跳过安装
)

echo [2/5] 构建前端 (vite build)...
call npm run build
if errorlevel 1 (
  echo [错误] 前端构建失败
  exit /b 1
)
if not exist "%ROOT%\frontend\dist\index.html" (
  echo [错误] 未找到 frontend\dist\index.html
  exit /b 1
)

echo [3/5] 发布后端 (dotnet publish Release)...
cd /d "%ROOT%\backend"
rem 清理旧输出时保留站点内遗留 App_Data（过渡期），但不再把库打进新包
if exist "%OUT%" (
  echo       清理旧输出中的程序文件（不删除外部 data）...
)
dotnet publish "%ROOT%\backend\ForumApi.csproj" -c Release -o "%OUT%" --nologo
if errorlevel 1 (
  echo [错误] 后端发布失败
  exit /b 1
)

echo [4/5] 合并前端到 wwwroot...
if not exist "%OUT%\wwwroot\" mkdir "%OUT%\wwwroot"
xcopy /E /Y /Q /I "%ROOT%\frontend\dist\*" "%OUT%\wwwroot\" >nul
if errorlevel 1 (
  echo [错误] 复制前端静态文件失败
  exit /b 1
)

echo [5/5] 清理误带库文件 + 注入环境...
rem 发布产物不得携带业务库
del /f /q "%OUT%\forum.db" >nul 2>&1
del /f /q "%OUT%\forum.db-wal" >nul 2>&1
del /f /q "%OUT%\forum.db-shm" >nul 2>&1
del /f /q "%OUT%\App_Data\forum.db" >nul 2>&1
del /f /q "%OUT%\App_Data\forum.db-wal" >nul 2>&1
del /f /q "%OUT%\App_Data\forum.db-shm" >nul 2>&1
del /f /q "%OUT%\App_Data\forum.dev.db" >nul 2>&1
del /f /q "%OUT%\App_Data\forum.dev.db-wal" >nul 2>&1
del /f /q "%OUT%\App_Data\forum.dev.db-shm" >nul 2>&1

if not exist "%OUT%\tools\" mkdir "%OUT%\tools"
xcopy /E /Y /Q /I "%ROOT%\publish\tools\*" "%OUT%\tools\" >nul

powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%\scripts\assert-no-db-in-publish.ps1" -PublishDir "%OUT%"
if errorlevel 1 exit /b 1

powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%\scripts\set-iis-env.ps1" -PublishDir "%OUT%" -DataDir "%DATA_DIR%"

rem 生产环境标记（IIS 也可在应用程序池 / web.config 设置）
if not exist "%OUT%\web.config" (
  echo [警告] 未生成 web.config，请确认已安装 ASP.NET Core Hosting Bundle
)

echo.
echo ========================================
echo   发布成功
echo ========================================
echo 输出目录: %OUT%
echo 数据目录: %DATA_DIR%\forum.db
echo 刷库工具: %OUT%\tools\DbSchemaMigrate.exe
echo.
echo 部署顺序提醒:
echo   1. 已自动刷库（也可单独: scripts\migrate-db.bat）
echo   2. 本脚本已发布程序
echo   3. 回收 IIS 应用程序池
echo.
echo 接下来在 IIS 中:
echo   1. 安装 .NET 8 Hosting Bundle
echo   2. 新建网站，物理路径指向输出目录（不要指向 data）
echo   3. 应用程序池: 无托管代码
echo   4. 给数据目录写权限:
echo      powershell -ExecutionPolicy Bypass -File "%ROOT%\scripts\fix-sqlite-permissions.ps1" -PublishDir "%OUT%" -DataDir "%DATA_DIR%"
echo   5. 编辑 %OUT%\appsettings.Production.json 修改 Jwt:Key
echo   6. 浏览站点验证
echo.
echo 也可指定 IIS 站点目录:
echo   publish-iis.bat "C:\inetpub\wwwroot\BSForum"
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%\scripts\fix-sqlite-permissions.ps1" -PublishDir "%OUT%" -DataDir "%DATA_DIR%"
explorer "%OUT%"
exit /b 0
