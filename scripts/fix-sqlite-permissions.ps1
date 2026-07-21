# Grant write access for SQLite (forum.db + WAL/SHM sidecars) under IIS.
# Usage:
#   powershell -ExecutionPolicy Bypass -File scripts\fix-sqlite-permissions.ps1 -PublishDir D:\BS\BS\publish\iis
#   powershell -ExecutionPolicy Bypass -File scripts\fix-sqlite-permissions.ps1 -PublishDir D:\BS\BS\publish\iis -AppPoolName BS
param(
  [Parameter(Mandatory = $true)]
  [string]$PublishDir,
  [string]$AppPoolName = ""
)

$ErrorActionPreference = "Continue"
$PublishDir = (Resolve-Path $PublishDir).Path

$appData = Join-Path $PublishDir "App_Data"
$logs = Join-Path $PublishDir "logs"
foreach ($d in @($appData, $logs)) {
  if (-not (Test-Path $d)) { New-Item -ItemType Directory -Path $d | Out-Null }
}

$idents = @("IIS_IUSRS", "IUSR")
if ($AppPoolName) {
  $idents += "IIS AppPool\$AppPoolName"
} else {
  # Try detect pool from site whose physical path matches
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
Write-Host "Granting Modify on App_Data + logs to: $($idents -join ', ')"

foreach ($id in $idents) {
  icacls $appData /grant "${id}:(OI)(CI)M" /T | Out-Null
  icacls $logs /grant "${id}:(OI)(CI)M" /T | Out-Null
  # Also parent dir so SQLite can create WAL next to db if path resolves oddly
  icacls $PublishDir /grant "${id}:(OI)(CI)M" | Out-Null
}

# Clear read-only attribute on db files if set
Get-ChildItem $appData -Filter "forum.db*" -ErrorAction SilentlyContinue | ForEach-Object {
  $_.Attributes = $_.Attributes -band (-bnot [IO.FileAttributes]::ReadOnly)
  Write-Host "cleared ReadOnly: $($_.Name)"
}

# Probe write
$probe = Join-Path $appData ".write_probe"
try {
  [IO.File]::WriteAllText($probe, [DateTime]::UtcNow.ToString("o"))
  Remove-Item $probe -Force -ErrorAction SilentlyContinue
  Write-Host "OK: App_Data is writable"
} catch {
  Write-Host "WARN: App_Data write probe failed: $($_.Exception.Message)"
  Write-Host "Manually grant Modify to IIS AppPool\<pool> on $appData"
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
