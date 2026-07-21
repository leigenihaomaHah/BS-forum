@echo off
chcp 65001 >nul
title BS Forum - 开发服务器

cd /d "%~dp0"

echo ============================================
echo   BS Forum - 启动开发环境（含 Mock 服务器）
echo ============================================
echo.

npm run dev:mock

echo.
pause
