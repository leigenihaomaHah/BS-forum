# 为发布目录的 web.config 注入:
# - Production 环境
# - BS_DATA_DIR（库在站点外，避免发布覆盖）
# - stdout 日志
# - 移除 WebDAV（否则 PUT/DELETE 会被 IIS 拦截成 405）
param(
  [Parameter(Mandatory = $true)]
  [string]$PublishDir,
  [string]$DataDir = ""
)

$webConfig = Join-Path $PublishDir "web.config"
if (-not (Test-Path $webConfig)) {
  Write-Host "skip: web.config not found"
  exit 0
}

$logsDir = Join-Path $PublishDir "logs"
if (-not (Test-Path $logsDir)) { New-Item -ItemType Directory -Path $logsDir | Out-Null }

if ($DataDir) {
  if (-not (Test-Path $DataDir)) { New-Item -ItemType Directory -Path $DataDir | Out-Null }
  $DataDir = (Resolve-Path $DataDir).Path
}

[xml]$xml = Get-Content $webConfig -Encoding UTF8
$sws = $xml.SelectSingleNode("//*[local-name()='system.webServer']")
if ($null -eq $sws) {
  Write-Host "skip: system.webServer node missing"
  exit 0
}

function Ensure-Child([System.Xml.XmlNode]$parent, [string]$name) {
  $n = $parent.SelectSingleNode("*[local-name()='$name']")
  if ($null -eq $n) {
    $n = $xml.CreateElement($name)
    [void]$parent.AppendChild($n)
  }
  return $n
}

function Set-EnvVar([System.Xml.XmlNode]$envVars, [string]$name, [string]$value) {
  $existing = $envVars.SelectNodes("*[local-name()='environmentVariable']") | Where-Object { $_.GetAttribute("name") -eq $name }
  if ($existing) {
    $existing.SetAttribute("value", $value)
  } else {
    $ev = $xml.CreateElement("environmentVariable")
    $ev.SetAttribute("name", $name)
    $ev.SetAttribute("value", $value)
    [void]$envVars.AppendChild($ev)
  }
}

$modules = Ensure-Child $sws "modules"
if (-not ($modules.SelectNodes("*[local-name()='remove']") | Where-Object { $_.GetAttribute("name") -eq "WebDAVModule" })) {
  $rm = $xml.CreateElement("remove")
  $rm.SetAttribute("name", "WebDAVModule")
  [void]$modules.AppendChild($rm)
}

$handlers = Ensure-Child $sws "handlers"
if (-not ($handlers.SelectNodes("*[local-name()='remove']") | Where-Object { $_.GetAttribute("name") -eq "WebDAV" })) {
  $rm = $xml.CreateElement("remove")
  $rm.SetAttribute("name", "WebDAV")
  $aspAdd = $handlers.SelectSingleNode("*[local-name()='add' and @name='aspNetCore']")
  if ($aspAdd) { [void]$handlers.InsertBefore($rm, $aspAdd) }
  else { [void]$handlers.AppendChild($rm) }
}

$security = Ensure-Child $sws "security"
$rf = Ensure-Child $security "requestFiltering"
$verbs = Ensure-Child $rf "verbs"
$verbs.SetAttribute("allowUnlisted", "true")

$aspNetCore = $sws.SelectSingleNode("*[local-name()='aspNetCore']")
if ($null -eq $aspNetCore) {
  Write-Host "skip: aspNetCore node missing"
  exit 0
}

$aspNetCore.SetAttribute("stdoutLogEnabled", "true")
$aspNetCore.SetAttribute("stdoutLogFile", ".\logs\stdout")

$envVars = $aspNetCore.SelectSingleNode("*[local-name()='environmentVariables']")
if ($null -eq $envVars) {
  $envVars = $xml.CreateElement("environmentVariables")
  [void]$aspNetCore.AppendChild($envVars)
}

Set-EnvVar $envVars "ASPNETCORE_ENVIRONMENT" "Production"
if ($DataDir) {
  Set-EnvVar $envVars "BS_DATA_DIR" $DataDir
  Write-Host "web.config: BS_DATA_DIR=$DataDir"
}

$xml.Save($webConfig)
Write-Host "web.config: Production + stdout + WebDAV disabled (PUT/DELETE OK)"
