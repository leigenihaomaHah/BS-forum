$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$pass = 0; $fail = 0; $skip = 0
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

$apiHost = if ($env:API_BASE) { $env:API_BASE.TrimEnd('/') } else { 'http://127.0.0.1:4080' }
$feHost  = if ($env:FE_BASE)  { $env:FE_BASE.TrimEnd('/') }  else { 'http://127.0.0.1:4173' }
$base = "$apiHost/api"
$skipSlow = $env:SKIP_SLOW -eq '1'

Write-Host "API  $apiHost"
Write-Host "FE   $feHost"
Write-Host ""

# ——— Bootstrap ———
Assert-Case 'api categories' {
  $r = Invoke-RestMethod -Uri "$base/categories"
  if (-not $r -or $r.Count -lt 1) { throw 'no categories' }
  if (-not $r[0].forums -or $r[0].forums.Count -lt 1) { throw 'no forums' }
  $script:forumId = [int]$r[0].forums[0].id
}

Assert-Case 'api levels' {
  $r = Invoke-RestMethod -Uri "$base/levels"
  if (-not $r -or $r.Count -lt 1) { throw 'no levels' }
}

Assert-Case 'api banners' {
  $null = Invoke-RestMethod -Uri "$base/banners"
}

Assert-Case 'api hot' {
  $null = Invoke-RestMethod -Uri "$base/hot"
}

Assert-Case 'login admin' {
  $script:admin = Invoke-ApiJson -Uri "$base/auth/login" -Method POST -Body '{"username":"admin","password":"admin123"}'
  if (-not $script:admin.token) { throw 'no token' }
  if (-not $script:admin.user.isAdmin) { throw 'not admin' }
  $script:adminHeaders = @{ Authorization = "Bearer $($script:admin.token)" }
  Write-Host "  admin id=$($script:admin.user.id) nick=$($script:admin.user.nickname)"
}

Assert-Case 'login demo' {
  $script:demo = Invoke-ApiJson -Uri "$base/auth/login" -Method POST -Body '{"username":"demo","password":"demo123"}'
  if (-not $script:demo.token) { throw 'no token' }
  $script:demoHeaders = @{ Authorization = "Bearer $($script:demo.token)" }
  Write-Host "  demo id=$($script:demo.user.id) nick=$($script:demo.user.nickname)"
}

Assert-Case 'login newbie' {
  $script:newbie = Invoke-ApiJson -Uri "$base/auth/login" -Method POST -Body '{"username":"newbie","password":"newbie123"}'
  if (-not $script:newbie.token) { throw 'no token' }
  $script:newbieHeaders = @{ Authorization = "Bearer $($script:newbie.token)" }
}

Assert-Case 'demo nickname not corrupted' {
  $nick = [string]$script:demo.user.nickname
  if ($nick -match '^\?+$') { throw "nickname corrupted: $nick" }
  if ([string]::IsNullOrWhiteSpace($nick)) { throw 'empty nickname' }
}

# ——— Auth / privacy / me ———
Assert-Case 'reset-password disabled' {
  try {
    Invoke-ApiJson -Uri "$base/auth/reset-password" -Method POST -Body '{"username":"demo","nickname":"demo","newPassword":"Demo12345x"}'
    throw 'should have failed'
  } catch {
    if ($_.Exception.Message -eq 'should have failed') { throw $_ }
    $info = Get-ErrInfo $_
    if ($info.Code -ne 400) { throw "expected 400 got $($info.Code) body=$($info.Body)" }
  }
}

Assert-Case 'me privacy fields' {
  $me = Invoke-RestMethod -Uri "$base/me" -Headers $demoHeaders
  foreach ($f in @('showPurchases','showFavorites','notifyReply','notifyMention')) {
    if ($me.PSObject.Properties.Name -notcontains $f) { throw "missing $f" }
  }
}

Assert-Case 'update privacy settings' {
  $r = Invoke-ApiJson -Uri "$base/me/settings" -Method PUT -Headers $demoHeaders -Body @{
    showPurchases = $false
    showFavorites = $false
    notifyReply = $true
    notifyMention = $true
  }
  if ($r.showPurchases -ne $false) { throw 'not saved' }
}

Assert-Case 'privacy purchases blocked for others' {
  try {
    Invoke-RestMethod -Uri "$base/users/$($admin.user.id)/purchases" -Headers $demoHeaders
    throw 'expected 403'
  } catch {
    if ($_.Exception.Message -eq 'expected 403') { throw $_ }
    $info = Get-ErrInfo $_
    if ($info.Code -ne 403 -and $info.Body -notmatch '未公开|隐私|Forbidden') {
      throw "status=$($info.Code) body=$($info.Body)"
    }
  }
}

Assert-Case 'sign-in status' {
  $null = Invoke-RestMethod -Uri "$base/me/sign-in-status" -Headers $demoHeaders
}

Assert-Case 'notifications summary' {
  $null = Invoke-RestMethod -Uri "$base/me/notifications/summary" -Headers $demoHeaders
}

# ——— Messages ———
Assert-Case 'messages conversations' {
  $null = Invoke-RestMethod -Uri "$base/messages/conversations" -Headers $demoHeaders
}

Assert-Case 'messages send' {
  $r = Invoke-ApiJson -Uri "$base/messages" -Method POST -Headers $demoHeaders -Body @{
    receiverId = [int]$admin.user.id
    content = "sys-test-$(Get-Date -Format HHmmss)"
  }
  if (-not $r.id) { throw 'no id' }
}

Assert-Case 'messages unread-count' {
  $r = Invoke-RestMethod -Uri "$base/messages/unread-count" -Headers $adminHeaders
  if ($r.PSObject.Properties.Name -notcontains 'count') { throw 'no count' }
}

# ——— Search / forums / threads ———
Assert-Case 'search with type filter' {
  $r = Invoke-RestMethod -Uri "$base/search?q=a&page=1&pageSize=5&type=public"
  if ($null -eq $r.items) { throw 'no items' }
}

Assert-Case 'forum threads list' {
  $r = Invoke-RestMethod -Uri "$base/forums/$forumId/threads?page=1&pageSize=5"
  if ($null -eq $r.items) { throw 'no items' }
  $script:sampleThreadId = if ($r.items.Count -gt 0) { [int]$r.items[0].id } else { 0 }
}

Assert-Case 'thread detail + posts' {
  if ($sampleThreadId -le 0) { Write-Host '  skip: no thread'; $script:skip++; return }
  $t = Invoke-RestMethod -Uri "$base/threads/$sampleThreadId"
  if (-not $t.id) { throw 'no thread' }
  $posts = Invoke-RestMethod -Uri "$base/threads/$sampleThreadId/posts?page=1&pageSize=5"
  if ($null -eq $posts.items) { throw 'no posts' }
  $p = $posts.items[0]
  if ($p.PSObject.Properties.Name -notcontains 'deleted') { throw 'missing deleted' }
  if ($p.PSObject.Properties.Name -notcontains 'editedAt') { throw 'missing editedAt' }
}

Assert-Case 'users search' {
  $r = Invoke-RestMethod -Uri "$base/users/search?q=a&limit=5" -Headers $adminHeaders
  if ($null -eq $r) { throw 'null' }
}

# ——— Lottery / shop ———
Assert-Case 'lottery config' {
  $r = Invoke-RestMethod -Uri "$base/lottery/config"
  if (-not $r.prizes -or $r.prizes.Count -lt 1) { throw 'no prizes' }
}

Assert-Case 'lottery status' {
  $r = Invoke-RestMethod -Uri "$base/lottery/status" -Headers $demoHeaders
  if ($r.PSObject.Properties.Name -notcontains 'coins') { throw 'no coins' }
}

Assert-Case 'lottery recent' {
  $null = Invoke-RestMethod -Uri "$base/lottery/recent?take=5"
}

Assert-Case 'lottery spin' {
  $st = Invoke-RestMethod -Uri "$base/lottery/status" -Headers $demoHeaders
  if ($st.remainingSpins -le 0) { Write-Host '  skip: no spins left'; $script:skip++; return }
  if (-not $st.freeAvailable -and -not $st.useTicketNext -and $st.coins -lt $st.costCoins) {
    Write-Host '  skip: cannot afford'; $script:skip++; return
  }
  $r = Invoke-ApiJson -Uri "$base/lottery/spin" -Method POST -Headers $demoHeaders
  if (-not $r.prizeId) { throw 'no prizeId' }
}

Assert-Case 'shop list' {
  $r = Invoke-RestMethod -Uri "$base/shop"
  if ($null -eq $r) { throw 'null' }
}

# ——— Admin ———
Assert-Case 'admin users list' {
  $r = Invoke-RestMethod -Uri "$base/admin/users?page=1&pageSize=20" -Headers $adminHeaders
  if (-not $r.items -or $r.items.Count -lt 1) { throw 'no users' }
  $demoRow = $r.items | Where-Object { $_.username -eq 'demo' } | Select-Object -First 1
  if ($demoRow -and ([string]$demoRow.nickname -match '^\?+$')) {
    throw "demo nickname corrupted in admin list: $($demoRow.nickname)"
  }
}

Assert-Case 'admin grant vip (no nickname rewrite)' {
  $null = Invoke-ApiJson -Uri "$base/admin/users/$($demo.user.id)" -Method PUT -Headers $adminHeaders -Body @{
    isVip = $true
    vipDays = 7
    vipTier = 1
  }
  $me = Invoke-RestMethod -Uri "$base/me" -Headers $demoHeaders
  if (-not $me.isVip) { throw 'vip not granted' }
  if ([string]$me.nickname -match '^\?+$') { throw "nickname corrupted after vip grant: $($me.nickname)" }
}

Assert-Case 'admin roles endpoint via users' {
  $r = Invoke-RestMethod -Uri "$base/admin/users?page=1&pageSize=5" -Headers $adminHeaders
  $u = $r.items[0]
  if ($u.PSObject.Properties.Name -notcontains 'role' -and $u.PSObject.Properties.Name -notcontains 'isAdmin') {
    throw 'missing role fields'
  }
}

Assert-Case 'admin moderators' {
  $null = Invoke-RestMethod -Uri "$base/admin/moderators" -Headers $adminHeaders
}

Assert-Case 'admin reports' {
  $r = Invoke-RestMethod -Uri "$base/admin/reports?pageSize=5" -Headers $adminHeaders
  if ($null -eq $r.items) { throw 'no items' }
}

Assert-Case 'admin settings' {
  $s = Invoke-RestMethod -Uri "$base/admin/settings" -Headers $adminHeaders
  if (-not $s.allow_register) { throw 'missing allow_register' }
}

Assert-Case 'admin recharge packages' {
  $pkgs = Invoke-RestMethod -Uri "$base/admin/recharge/packages" -Headers $adminHeaders
  if (-not $pkgs -or $pkgs.Count -lt 1) { throw 'no packages' }
  $script:pkgId = [int]$pkgs[0].id
}

Assert-Case 'admin generate cards' {
  $r = Invoke-ApiJson -Uri "$base/admin/recharge/cards/generate" -Method POST -Headers $adminHeaders -Body @{
    packageId = $pkgId
    count = 2
  }
  if (-not $r -or $r.Count -lt 2) { throw "count=$($r.Count)" }
}

Assert-Case 'admin shop' {
  try {
    $null = Invoke-RestMethod -Uri "$base/admin/shop" -Headers $adminHeaders
  } catch {
    # some builds use /admin/shop/items
    $null = Invoke-RestMethod -Uri "$base/admin/shop/items" -Headers $adminHeaders
  }
}

Assert-Case 'admin threads pending/review' {
  $null = Invoke-RestMethod -Uri "$base/admin/threads?page=1&pageSize=5" -Headers $adminHeaders
}

Assert-Case 'allow_register enforced' {
  $null = Invoke-ApiJson -Uri "$base/admin/settings" -Method PUT -Headers $adminHeaders -Body @{
    settings = @{ allow_register = '0' }
  }
  try {
    $cap = Invoke-RestMethod -Uri "$base/auth/captcha"
    try {
      Invoke-ApiJson -Uri "$base/auth/register" -Method POST -Body @{
        username = "x$(Get-Random -Maximum 99999)"
        password = 'Test12345'
        nickname = 'x'
        captchaId = $cap.captchaId
        captchaCode = '0000'
      }
      throw 'register should be blocked'
    } catch {
      if ($_.Exception.Message -eq 'register should be blocked') { throw $_ }
      $info = Get-ErrInfo $_
      if ($info.Code -ne 400) { throw "expected 400 got $($info.Code) $($info.Body)" }
    }
  } finally {
    $null = Invoke-ApiJson -Uri "$base/admin/settings" -Method PUT -Headers $adminHeaders -Body @{
      settings = @{ allow_register = '1' }
    }
  }
}

# ——— Frontend ———
Assert-Case 'frontend home' {
  $html = (Invoke-WebRequest -Uri "$feHost/" -UseBasicParsing -TimeoutSec 10).Content
  if ($html -notmatch 'id="app"|vite') { throw 'unexpected html' }
}

Assert-Case 'frontend proxy /api' {
  $r = Invoke-RestMethod -Uri "$feHost/api/categories" -TimeoutSec 10
  if (-not $r -or $r.Count -lt 1) { throw 'proxy failed' }
}

Assert-Case 'frontend lottery route' {
  $null = Invoke-WebRequest -Uri "$feHost/lottery" -UseBasicParsing -TimeoutSec 10
}

Assert-Case 'frontend messages route' {
  $null = Invoke-WebRequest -Uri "$feHost/messages" -UseBasicParsing -TimeoutSec 10
}

Assert-Case 'frontend search route' {
  $null = Invoke-WebRequest -Uri "$feHost/search?q=test" -UseBasicParsing -TimeoutSec 10
}

Assert-Case 'frontend admin route shell' {
  $null = Invoke-WebRequest -Uri "$feHost/admin/moderators" -UseBasicParsing -TimeoutSec 10
}

# ——— Slow: soft-delete ———
Assert-Case 'soft-delete reply' {
  if ($skipSlow) { Write-Host '  skip: SKIP_SLOW=1'; $script:skip++; return }
  $create = Invoke-ApiJson -Uri "$base/threads" -Method POST -Headers $demoHeaders -Body @{
    forumId = [int]$forumId
    title = "sys-soft-$(Get-Date -Format HHmmss)"
    content = "soft delete system test body content ok"
    type = 'public'
  }
  $tid = $create.id
  Write-Host '  waiting 16s for reply cooldown...'
  Start-Sleep -Seconds 16
  $post = Invoke-ApiJson -Uri "$base/threads/$tid/replies" -Method POST -Headers $demoHeaders -Body @{
    content = 'reply for soft delete test xx'
  }
  $null = Invoke-RestMethod -Uri "$base/posts/$($post.id)" -Method DELETE -Headers $demoHeaders
  $posts = Invoke-RestMethod -Uri "$base/threads/$tid/posts?page=1&pageSize=20"
  $gone = $posts.items | Where-Object { $_.id -eq $post.id }
  if (-not $gone) { throw 'soft-deleted post missing from list' }
  if (-not $gone.deleted) { throw 'deleted flag false' }
}

Write-Host ""
Write-Host ("==== RESULT: {0} passed, {1} failed, {2} skipped ====" -f $pass, $fail, $skip) -ForegroundColor $(if ($fail -eq 0) { 'Green' } else { 'Yellow' })
if ($fail -gt 0) { exit 1 }
