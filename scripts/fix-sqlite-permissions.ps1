# Grant write access for SQLite under IIS (external data dir + site logs).
# Usage:
#   powershell -ExecutionPolicy Bypass -File scripts\fix-sqlite-permissions.ps1 -PublishDir D:\BS\BS\publish\iis -DataDir D:\BS\BS\data
param(
  [Parameter(Mandatory = $true)]
  [string]$PublishDir,
  [string]$DataDir = "",
  [string]$AppPoolName = ""
)

$ErrorActionPreference = "Continue"
$PublishDir = (Resolve-Path $PublishDir).Path

if (-not $DataDir) {
  $DataDir = $env:BS_DATA_DIR
}
if (-not $DataDir) {
  # Default: sibling ../data relative to publish/iis when using repo publish layout
  $candidate = Join-Path (Split-Path (Split-Path $PublishDir -Parent) -Parent) "data"
  if (Test-Path $candidate) { $DataDir = $candidate }
  else { $DataDir = Join-Path $PublishDir "App_Data" }
}

if (-not (Test-Path $DataDir)) { New-Item -ItemType Directory -Path $DataDir | Out-Null }
$DataDir = (Resolve-Path $DataDir).Path

$logs = Join-Path $PublishDir "logs"
$legacyAppData = Join-Path $PublishDir "App_Data"
foreach ($d in @($DataDir, $logs, $legacyAppData)) {
  if (-not (Test-Path $d)) { New-Item -ItemType Directory -Path $d | Out-Null }
}

$idents = @("IIS_IUSRS", "IUSR")
if ($AppPoolName) {
  $idents += "IIS AppPool\$AppPoolName"
} else {
  try {
    Import-Module WebAdministration -ErrorAction Stop
    Get-Website | ForEach-Object {
      $phys = $_.physicalPath
      if ($phys -and ((Resolve-Path $phys -ErrorAction SilentlyContinue).Path -eq $PublishDir)) {
        $idents += "IIS AppPool\$($_.applicationPool)"
        if (-not $AppPoolName) { $script:AppPoolName = $_.applicationPool }
      }
    }
  } catch {}
}

$idents = $idents | Select-Object -Unique
Write-Host "Granting Modify on DataDir + logs to: $($idents -join ', ')"
Write-Host "DataDir: $DataDir"

foreach ($id in $idents) {
  icacls $DataDir /grant "${id}:(OI)(CI)M" /T | Out-Null
  icacls $logs /grant "${id}:(OI)(CI)M" /T | Out-Null
  icacls $legacyAppData /grant "${id}:(OI)(CI)M" /T | Out-Null
  icacls $PublishDir /grant "${id}:(OI)(CI)M" | Out-Null
}

Get-ChildItem $DataDir -Filter "forum.db*" -ErrorAction SilentlyContinue | ForEach-Object {
  $_.Attributes = $_.Attributes -band (-bnot [IO.FileAttributes]::ReadOnly)
  Write-Host "cleared ReadOnly: $($_.FullName)"
}
Get-ChildItem $legacyAppData -Filter "forum.db*" -ErrorAction SilentlyContinue | ForEach-Object {
  $_.Attributes = $_.Attributes -band (-bnot [IO.FileAttributes]::ReadOnly)
  Write-Host "cleared ReadOnly: $($_.FullName)"
}

$probe = Join-Path $DataDir ".write_probe"
try {
  [IO.File]::WriteAllText($probe, [DateTime]::UtcNow.ToString("o"))
  Remove-Item $probe -Force -ErrorAction SilentlyContinue
  Write-Host "OK: DataDir is writable"
} catch {
  Write-Host "WARN: DataDir write probe failed: $($_.Exception.Message)"
  Write-Host "Manually grant Modify to IIS AppPool\<pool> on $DataDir"
  exit 1
}

if ($AppPoolName) {
  try {
    Import-Module WebAdministration -ErrorAction Stop
    Restart-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
    Write-Host "Recycled app pool: $AppPoolName"
  } catch {}
}

Write-Host "Done."
