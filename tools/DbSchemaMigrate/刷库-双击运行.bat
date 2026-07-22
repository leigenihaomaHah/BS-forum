@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo 正在刷库（自动使用 ..\data\forum.db）...
echo.
DbSchemaMigrate.exe
echo.
pause
