# Called by .github/workflows/deploy.yml on the self-hosted runner (runs as LocalSystem).
# Copies freshly built API + frontend into C:\nexsus and restarts them.
#
# Server-only things that are NEVER overwritten:
#   API:      appsettings.Production.json (real connection string / JWT key / CORS), web.config,
#             files\, private-files\, logs\
#   Frontend: ecosystem.config.js (pm2), web.config (IIS reverse proxy), logs\

param(
    [Parameter(Mandatory)][string]$ApiBuild,
    [Parameter(Mandatory)][string]$FrontendBuild,
    [string]$Commit = 'manual'
)

$ErrorActionPreference = 'Stop'

$apiDir  = 'C:\nexsus\nexsus-api'
$feDir   = 'C:\nexsus\nexsus-frontend'
$backups = 'C:\nexsus\backups'
$apiHost = 'ugc-student-api.website-link.info'
$feHost  = 'ugc-student-portal.website-link.info'
$node    = 'C:\Program Files\nodejs\node.exe'
$pm2     = 'C:\Users\anamuliut\AppData\Roaming\npm\node_modules\pm2\bin\pm2'
$env:PM2_HOME = 'C:\ProgramData\pm2\home'

function Step($m) { Write-Host "== $m" -ForegroundColor Cyan }

# robocopy exit codes 0-7 are success, 8+ are failures
function Copy-Dir($from, $to, [string[]]$extra) {
    robocopy $from $to /E /R:5 /W:2 /NFL /NDL /NP /NJH @extra | Out-Host
    if ($LASTEXITCODE -ge 8) { throw "robocopy $from -> $to failed ($LASTEXITCODE)" }
    $global:LASTEXITCODE = 0
}

function Pm2 { & $node $pm2 @args | Out-Host }

# Safety net: the server settings must exist, or the API would start with the repo's dev settings
if (-not (Test-Path "$apiDir\appsettings.Production.json")) {
    throw "$apiDir\appsettings.Production.json is missing - refusing to deploy."
}

# ---------------------------------------------------------------- backup
Step 'Backup current version'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$bk = Join-Path $backups $stamp
Copy-Dir $apiDir "$bk\api" @('/XD', 'files', 'private-files', 'logs')
Copy-Dir "$feDir\.next" "$bk\frontend-next" @('/XD', 'cache', 'dev')
# keep the newest 5 backups
Get-ChildItem $backups -Directory | Sort-Object Name -Descending | Select-Object -Skip 5 |
    Remove-Item -Recurse -Force

# ---------------------------------------------------------------- API
Step 'Deploy API'
# app_offline.htm makes IIS/ANCM shut the in-process app down so its DLLs are unlocked
Set-Content "$apiDir\app_offline.htm" '<h1>Updating - back in a few seconds</h1>' -Encoding UTF8
Start-Sleep -Seconds 5
try {
    Copy-Dir $ApiBuild $apiDir @('/XF', 'appsettings.Production.json', 'appsettings.Development.json',
                                 'web.config', 'app_offline.htm',
                                 '/XD', 'files', 'private-files', 'logs')
}
finally {
    Remove-Item "$apiDir\app_offline.htm" -Force -ErrorAction SilentlyContinue
}

# ---------------------------------------------------------------- Frontend
Step 'Deploy frontend'
Pm2 stop nexsus-frontend
try {
    # /MIR so removed pages/packages disappear; the excluded dirs are left alone
    Copy-Dir "$FrontendBuild\.next"        "$feDir\.next"        @('/MIR', '/XD', 'cache', 'dev')
    Copy-Dir "$FrontendBuild\node_modules" "$feDir\node_modules" @('/MIR')
    if (Test-Path "$FrontendBuild\public") {
        Copy-Dir "$FrontendBuild\public"   "$feDir\public"       @('/MIR')
    }
    foreach ($f in 'package.json', 'package-lock.json', 'next.config.mjs') {
        Copy-Item "$FrontendBuild\$f" "$feDir\$f" -Force
    }
}
finally {
    Push-Location $feDir
    Pm2 delete nexsus-frontend
    Pm2 start ecosystem.config.js
    Pm2 save
    Pop-Location
}

Set-Content 'C:\nexsus\DEPLOYED_VERSION.txt' "$Commit  $(Get-Date -Format s)"

# ---------------------------------------------------------------- smoke tests
Step 'Smoke tests'
$failed = 0
$checks = @(
    @{ h = $apiHost; p = '/swagger/index.html' },
    @{ h = $feHost;  p = '/' },
    @{ h = $feHost;  p = '/login' }
)
foreach ($t in $checks) {
    $ok = $false
    for ($i = 1; $i -le 6 -and -not $ok; $i++) {
        try {
            $r = Invoke-WebRequest "http://127.0.0.1$($t.p)" -Headers @{ Host = $t.h } -UseBasicParsing -TimeoutSec 60
            $ok = $r.StatusCode -eq 200
        } catch { Start-Sleep -Seconds 10 }
    }
    if ($ok) { Write-Host "OK   $($t.h)$($t.p)" -ForegroundColor Green }
    else     { Write-Host "FAIL $($t.h)$($t.p)" -ForegroundColor Red; $failed++ }
}
if ($failed) { throw "$failed smoke test(s) failed. Previous version is in $bk" }

Write-Host "Deployed $Commit" -ForegroundColor Green
