# Repair VPS Postgres + upload API production settings, then restart jamago-api.
# Usage: powershell -ExecutionPolicy Bypass -File scripts/fix-vps-db.ps1

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$prodSettings = Join-Path $root "deploy\appsettings.Production.json"
$fixScript = Join-Path $root "deploy\fix-vps-db.sh"
$remoteHost = "root@76.13.133.53"
$remoteApp = "/var/www/jamago-api"

if (-not (Test-Path $prodSettings)) {
  Write-Error "Missing $prodSettings"
}

if (-not (Test-Path $fixScript)) {
  Write-Error "Missing $fixScript"
}

Write-Host "This will:" -ForegroundColor Cyan
Write-Host "  1) Ensure Postgres role/db jamago_admin / jamago_db (password Jamago@123)"
Write-Host "  2) Upload local appsettings.Production.json"
Write-Host "  3) Restart jamago-api and test staff + login endpoints"
Write-Host ""
Write-Host "Enter VPS root password when prompted (3 times: scp, scp, ssh)." -ForegroundColor Yellow

Write-Host "Uploading Production settings..." -ForegroundColor Cyan
scp -o StrictHostKeyChecking=accept-new $prodSettings "${remoteHost}:${remoteApp}/appsettings.Production.json"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Uploading repair script..." -ForegroundColor Cyan
scp $fixScript "${remoteHost}:/tmp/fix-vps-db.sh"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Running repair script on VPS..." -ForegroundColor Cyan
# Strip Windows CRLF so bash does not fail
ssh $remoteHost "sed -i 's/\r`$//' /tmp/fix-vps-db.sh && chmod +x /tmp/fix-vps-db.sh && bash /tmp/fix-vps-db.sh"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "Done. Try login at https://jamago.qa/admin/login" -ForegroundColor Green
Write-Host "  Email: admin@jamago.qa" -ForegroundColor Green
Write-Host "  Password: Admin@jamago2026!" -ForegroundColor Green
