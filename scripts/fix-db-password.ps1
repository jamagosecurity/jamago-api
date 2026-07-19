# Sync jamago_admin Postgres password on the VPS to match local Production settings.
# Usage: powershell -ExecutionPolicy Bypass -File scripts/fix-db-password.ps1

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$prodSettings = Join-Path $root "deploy\appsettings.Production.json"
$remoteHost = "root@76.13.133.53"

if (-not (Test-Path $prodSettings)) {
  Write-Error "Missing $prodSettings"
}

$raw = Get-Content $prodSettings -Raw
if ($raw -notmatch 'Password=([^";]+)') {
  Write-Error "Could not read Postgres password from Production settings."
}

$dbPass = $Matches[1]
if ([string]::IsNullOrWhiteSpace($dbPass) -or $dbPass -eq "YOUR_PASSWORD_HERE") {
  Write-Error "Production Postgres password is still a placeholder."
}

# Escape single quotes for SQL
$sqlPass = $dbPass.Replace("'", "''")

Write-Host "Updating jamago_admin password on VPS to match Production settings..." -ForegroundColor Cyan
Write-Host "Enter your VPS root password when prompted." -ForegroundColor Yellow

$remoteCmd = @"
set -e
sudo -u postgres psql -v ON_ERROR_STOP=1 -c "ALTER USER jamago_admin WITH PASSWORD '$sqlPass';"
sudo -u postgres psql -v ON_ERROR_STOP=1 -c "ALTER DATABASE jamago_db OWNER TO jamago_admin;" 2>/dev/null || true
systemctl restart jamago-api
sleep 2
systemctl --no-pager --full status jamago-api || true
curl -sS -o /dev/null -w "local_api=%{http_code}\n" http://127.0.0.1:5093/api/staff || true
curl -sS -o /dev/null -w "public_api=%{http_code}\n" https://jamago.qa/api/staff || true
"@

ssh $remoteHost $remoteCmd
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "DB password synced and API restarted." -ForegroundColor Green
Write-Host "Check: https://jamago.qa/api/staff" -ForegroundColor Green
