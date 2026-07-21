@echo off
chcp 65001 >nul
setlocal EnableExtensions EnableDelayedExpansion

rem ============================================================
rem  BS Forum 一键发布到 IIS
rem  用法:
rem    publish-iis.bat
rem    publish-iis.bat D:\inetpub\wwwroot\BSForum
rem ============================================================

set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"

set "OUT=%ROOT%\publish\iis"
if not "%~1"=="" set "OUT=%~1"

echo.
echo ========================================
echo   BS Forum IIS 一键发布
echo ========================================
echo 项目目录: %ROOT%
echo 输出目录: %OUT%
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

echo [1/4] 检查前端依赖...
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

echo [2/4] 构建前端 (vite build)...
call npm run build
if errorlevel 1 (
  echo [错误] 前端构建失败
  exit /b 1
)
if not exist "%ROOT%\frontend\dist\index.html" (
  echo [错误] 未找到 frontend\dist\index.html
  exit /b 1
)

echo [3/4] 发布后端 (dotnet publish Release)...
cd /d "%ROOT%\backend"
if exist "%OUT%" (
  echo       清理旧输出...
  rem 保留已有数据库，避免覆盖线上数据
  if exist "%OUT%\App_Data\forum.db" (
    if not exist "%TEMP%\bsforum-db-backup\" mkdir "%TEMP%\bsforum-db-backup" >nul 2>&1
    copy /Y "%OUT%\App_Data\forum.db" "%TEMP%\bsforum-db-backup\forum.db" >nul
    set "HAS_DB_BACKUP=1"
  )
)
dotnet publish "%ROOT%\backend\ForumApi.csproj" -c Release -o "%OUT%" --nologo
if errorlevel 1 (
  echo [错误] 后端发布失败
  exit /b 1
)

echo [4/4] 合并前端到 wwwroot...
if not exist "%OUT%\wwwroot\" mkdir "%OUT%\wwwroot"
xcopy /E /Y /Q /I "%ROOT%\frontend\dist\*" "%OUT%\wwwroot\" >nul
if errorlevel 1 (
  echo [错误] 复制前端静态文件失败
  exit /b 1
)

if not exist "%OUT%\App_Data\" mkdir "%OUT%\App_Data"

powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%\scripts\set-iis-env.ps1" -PublishDir "%OUT%"

if defined HAS_DB_BACKUP (
  if exist "%TEMP%\bsforum-db-backup\forum.db" (
    copy /Y "%TEMP%\bsforum-db-backup\forum.db" "%OUT%\App_Data\forum.db" >nul
    attrib -R "%OUT%\App_Data\forum.db" >nul 2>&1
    echo       已还原原有 forum.db
  )
)

rem 生产环境标记（IIS 也可在应用程序池 / web.config 设置）
if not exist "%OUT%\web.config" (
  echo [警告] 未生成 web.config，请确认已安装 ASP.NET Core Hosting Bundle
)

echo.
echo ========================================
echo   发布成功
echo ========================================
echo 输出目录: %OUT%
echo.
echo 接下来在 IIS 中:
echo   1. 安装 .NET 8 Hosting Bundle
echo      https://dotnet.microsoft.com/permalink/dotnetcore-current-windows-runtime-bundle-installer
echo   2. 新建网站或应用程序，物理路径指向上述输出目录
echo   3. 应用程序池: 无托管代码 ^| .NET CLR 版本设为「无托管代码」
echo   4. 给应用程序池写权限 (必做，否则新增分类会报 readonly database):
echo      powershell -ExecutionPolicy Bypass -File "%ROOT%\scripts\fix-sqlite-permissions.ps1" -PublishDir "%OUT%"
echo   5. 编辑 %OUT%\appsettings.Production.json 修改 Jwt:Key
echo   6. 浏览站点验证登录 / 发帖
echo.
echo 也可直接指定 IIS 站点目录再发布一次:
echo   publish-iis.bat "C:\inetpub\wwwroot\BSForum"
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%\scripts\fix-sqlite-permissions.ps1" -PublishDir "%OUT%"
explorer "%OUT%"
exit /b 0
