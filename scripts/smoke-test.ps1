$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$pass = 0; $fail = 0
function Assert-Case([string]$name, [scriptblock]$script) {
  try {
    & $script
    Write-Host "[PASS] $name" -ForegroundColor Green
    $script:pass++
  } catch {
    Write-Host "[FAIL] $name : $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) { Write-Host "       $($_.ErrorDetails.Message)" -ForegroundColor DarkRed }
    $script:fail++
  }
}

# Windows PowerShell 5 默认用系统代码页发 Body，中文会被写成 ??? 并污染数据库
function Invoke-ApiJson {
  param(
    [string]$Uri,
    [string]$Method = 'GET',
    [hashtable]$Headers = $null,
    $Body = $null
  )
  $params = @{ Uri = $Uri; Method = $Method }
  if ($Headers) { $params.Headers = $Headers }
  if ($null -ne $Body) {
    $json = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Compress -Depth 8 }
    $params.ContentType = 'application/json; charset=utf-8'
    $params.Body = [System.Text.Encoding]::UTF8.GetBytes($json)
  }
  return Invoke-RestMethod @params
}

$apiHost = if ($env:API_BASE) { $env:API_BASE.TrimEnd('/') } else { 'http://127.0.0.1:4080' }
$base = "$apiHost/api"

$admin = Invoke-RestMethod -Uri "$base/auth/login" -Method POST -ContentType 'application/json' -Body '{"username":"admin","password":"admin123"}'
$adminHeaders = @{ Authorization = "Bearer $($admin.token)" }
Write-Host "Admin id=$($admin.user.id)"

$demo = Invoke-RestMethod -Uri "$base/auth/login" -Method POST -ContentType 'application/json' -Body '{"username":"demo","password":"demo123"}'
$demoHeaders = @{ Authorization = "Bearer $($demo.token)" }
Write-Host "Demo id=$($demo.user.id)"

function Get-ErrInfo($err) {
  $code = 0
  $body = ''
  try {
    if ($err.Exception.Response) {
      $code = [int]$err.Exception.Response.StatusCode
      $stream = $err.Exception.Response.GetResponseStream()
      if ($stream) {
        $reader = New-Object System.IO.StreamReader($stream)
        $body = $reader.ReadToEnd()
      }
    }
  } catch {}
  if (-not $body -and $err.ErrorDetails.Message) { $body = $err.ErrorDetails.Message }
  if (-not $body) { $body = $err.Exception.Message }
  return @{ Code = $code; Body = $body }
}

Assert-Case 'reset-password disabled' {
  try {
    Invoke-RestMethod -Uri "$base/auth/reset-password" -Method POST -ContentType 'application/json' -Body '{"username":"demo","nickname":"demo","newPassword":"Demo12345x"}'
    throw 'should have failed'
  } catch {
    $info = Get-ErrInfo $_
    if ($info.Code -ne 400) { throw "expected 400 got $($info.Code) body=$($info.Body)" }
    if ($info.Body -notmatch 'message') { throw "no message: $($info.Body)" }
  }
}

Assert-Case 'privacy purchases blocked for others' {
  try {
    Invoke-RestMethod -Uri "$base/users/$($admin.user.id)/purchases" -Headers $demoHeaders
    throw 'expected 403'
  } catch {
    $info = Get-ErrInfo $_
    if ($info.Code -ne 403 -and $info.Body -notmatch '未公开|隐私|Forbidden') {
      throw "status=$($info.Code) body=$($info.Body)"
    }
  }
}

Assert-Case 'messages conversations' {
  $null = Invoke-RestMethod -Uri "$base/messages/conversations" -Headers $demoHeaders
}

Assert-Case 'messages send' {
  $body = @{ receiverId = [int]$admin.user.id; content = "smoke-$(Get-Date -Format HHmmss)" } | ConvertTo-Json
  $r = Invoke-RestMethod -Uri "$base/messages" -Method POST -Headers $demoHeaders -ContentType 'application/json' -Body $body
  if (-not $r.id) { throw 'no id' }
}

Assert-Case 'messages unread-count' {
  $r = Invoke-RestMethod -Uri "$base/messages/unread-count" -Headers $adminHeaders
  if (-not ($r.PSObject.Properties.Name -contains 'count')) { throw 'no count' }
}

Assert-Case 'search with type filter' {
  $r = Invoke-RestMethod -Uri "$base/search?q=a&page=1&pageSize=5&type=public"
  if ($null -eq $r.items) { throw 'no items' }
}

Assert-Case 'me privacy fields' {
  $me = Invoke-RestMethod -Uri "$base/me" -Headers $demoHeaders
  foreach ($f in @('showPurchases','showFavorites','notifyReply','notifyMention')) {
    if ($me.PSObject.Properties.Name -notcontains $f) { throw "missing $f (old binary?)" }
  }
}

Assert-Case 'update privacy settings' {
  $body = '{"showPurchases":false,"showFavorites":false,"notifyReply":true,"notifyMention":true}'
  $r = Invoke-RestMethod -Uri "$base/me/settings" -Method PUT -Headers $demoHeaders -ContentType 'application/json' -Body $body
  if ($r.showPurchases -ne $false) { throw 'not saved' }
}

Assert-Case 'admin grant vip' {
  # 不要回写 nickname：PS5 非 UTF-8 Body 会把中文昵称污染成 ????
  $null = Invoke-ApiJson -Uri "$base/admin/users/$($demo.user.id)" -Method PUT -Headers $adminHeaders -Body @{
    isVip = $true
    vipDays = 7
    vipTier = 1
  }
  $me = Invoke-RestMethod -Uri "$base/me" -Headers $demoHeaders
  if (-not $me.isVip) { throw 'vip not granted' }
}

Assert-Case 'admin generate cards' {
  $pkgs = Invoke-RestMethod -Uri "$base/admin/recharge/packages" -Headers $adminHeaders
  if (-not $pkgs -or $pkgs.Count -lt 1) { throw 'no packages' }
  $body = @{ packageId = [int]$pkgs[0].id; count = 2 } | ConvertTo-Json
  $r = Invoke-RestMethod -Uri "$base/admin/recharge/cards/generate" -Method POST -Headers $adminHeaders -ContentType 'application/json' -Body $body
  if (-not $r -or $r.Count -lt 2) { throw "count=$($r.Count)" }
}

Assert-Case 'admin settings allow_register' {
  $s = Invoke-RestMethod -Uri "$base/admin/settings" -Headers $adminHeaders
  if (-not $s.allow_register) { throw 'missing allow_register' }
}

Assert-Case 'post dto deleted/editedAt' {
  $cats = Invoke-RestMethod -Uri "$base/categories"
  $forumId = $cats[0].forums[0].id
  $threads = Invoke-RestMethod -Uri "$base/forums/$forumId/threads?page=1&pageSize=1"
  if (-not $threads.items -or $threads.items.Count -lt 1) { Write-Host '  skip: no threads'; return }
  $tid = $threads.items[0].id
  $posts = Invoke-RestMethod -Uri "$base/threads/$tid/posts?page=1&pageSize=5"
  $p = $posts.items[0]
  if ($p.PSObject.Properties.Name -notcontains 'deleted') { throw 'missing deleted' }
  if ($p.PSObject.Properties.Name -notcontains 'editedAt') { throw 'missing editedAt' }
}

Assert-Case 'admin moderators' {
  $null = Invoke-RestMethod -Uri "$base/admin/moderators" -Headers $adminHeaders
}

Assert-Case 'admin reports' {
  $r = Invoke-RestMethod -Uri "$base/admin/reports?pageSize=5" -Headers $adminHeaders
  if ($null -eq $r.items) { throw 'no items' }
}

Assert-Case 'frontend messages route' {
  $html = (Invoke-WebRequest -Uri 'http://localhost:5173/messages' -UseBasicParsing).Content
  if ($html -notmatch 'id="app"|vite') { throw 'unexpected html' }
}

Assert-Case 'frontend search route' {
  $null = Invoke-WebRequest -Uri 'http://localhost:5173/search?q=test' -UseBasicParsing
}

Assert-Case 'soft-delete reply' {
  $cats = Invoke-RestMethod -Uri "$base/categories"
  $forumId = $cats[0].forums[0].id
  $create = @{
    forumId = [int]$forumId
    title = "smoke-soft-$(Get-Date -Format HHmmss)"
    content = "soft delete smoke body content ok"
    type = 'public'
  } | ConvertTo-Json
  try {
    $th = Invoke-RestMethod -Uri "$base/threads" -Method POST -Headers $demoHeaders -ContentType 'application/json' -Body $create
  } catch {
    $info = Get-ErrInfo $_
    throw "create thread failed: $($info.Body)"
  }
  $tid = $th.id
  Write-Host '  waiting 16s for reply cooldown...'
  Start-Sleep -Seconds 16
  $reply = @{ content = 'reply for soft delete test xx' } | ConvertTo-Json
  try {
    $post = Invoke-RestMethod -Uri "$base/threads/$tid/replies" -Method POST -Headers $demoHeaders -ContentType 'application/json' -Body $reply
  } catch {
    $info = Get-ErrInfo $_
    throw "reply failed: $($info.Body)"
  }
  $null = Invoke-RestMethod -Uri "$base/posts/$($post.id)" -Method DELETE -Headers $demoHeaders
  $posts = Invoke-RestMethod -Uri "$base/threads/$tid/posts?page=1&pageSize=20"
  $gone = $posts.items | Where-Object { $_.id -eq $post.id }
  if (-not $gone) { throw 'soft-deleted post missing from list' }
  if (-not $gone.deleted) { throw 'deleted flag false' }
}

Assert-Case 'allow_register enforced' {
  $off = @{ settings = @{ allow_register = '0' } } | ConvertTo-Json -Compress -Depth 5
  $on = @{ settings = @{ allow_register = '1' } } | ConvertTo-Json -Compress -Depth 5
  $null = Invoke-RestMethod -Uri "$base/admin/settings" -Method PUT -Headers $adminHeaders -ContentType 'application/json' -Body $off
  try {
    $cap = Invoke-RestMethod -Uri "$base/auth/captcha"
    $body = @{
      username = "x$(Get-Random -Maximum 99999)"
      password = 'Test12345'
      nickname = 'x'
      captchaId = $cap.captchaId
      captchaCode = '0000'
    } | ConvertTo-Json
    try {
      Invoke-RestMethod -Uri "$base/auth/register" -Method POST -ContentType 'application/json' -Body $body
      throw 'register should be blocked'
    } catch {
      if ($_.Exception.Message -eq 'register should be blocked') { throw $_ }
      $info = Get-ErrInfo $_
      if ($info.Code -ne 400) { throw "expected 400 got $($info.Code) $($info.Body)" }
    }
  } finally {
    $null = Invoke-RestMethod -Uri "$base/admin/settings" -Method PUT -Headers $adminHeaders -ContentType 'application/json' -Body $on
  }
}

Write-Host ""
Write-Host ("==== RESULT: {0} passed, {1} failed ====" -f $pass, $fail) -ForegroundColor $(if ($fail -eq 0) { 'Green' } else { 'Yellow' })
if ($fail -gt 0) { exit 1 }

