# Fail publish if any business SQLite DB leaked into the site package.
param(
  [Parameter(Mandatory = $true)]
  [string]$PublishDir
)

$ErrorActionPreference = "Stop"
$PublishDir = (Resolve-Path $PublishDir).Path

$hits = Get-ChildItem -Path $PublishDir -Recurse -File -ErrorAction SilentlyContinue |
  Where-Object {
    $_.Name -match '^(forum|forum\.dev)\.db(-wal|-shm)?$' -or
    ($_.Extension -eq '.db' -and $_.DirectoryName -match '[\\/]App_Data$')
  }

if ($hits) {
  Write-Host "[错误] 发布目录含有数据库文件（禁止打进包，以免覆盖线上数据）:" -ForegroundColor Red
  $hits | ForEach-Object { Write-Host "  $($_.FullName)" }
  exit 1
}

Write-Host "OK: publish package contains no forum.db"
exit 0
