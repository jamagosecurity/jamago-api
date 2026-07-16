# Deploy frontend + backend to Hostinger VPS (jamago.qa)
# Usage: powershell -ExecutionPolicy Bypass -File scripts/deploy-all.ps1

$ErrorActionPreference = "Stop"

$apiRoot = "F:\JamGo\jamago-api"
$webRoot = "F:\JamGo\jamago-security"

Write-Host "=== 1/2 Backend API ===" -ForegroundColor Cyan
powershell -ExecutionPolicy Bypass -File (Join-Path $apiRoot "scripts\deploy.ps1")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "=== 2/2 Frontend ===" -ForegroundColor Cyan
powershell -ExecutionPolicy Bypass -File (Join-Path $webRoot "scripts\deploy.ps1")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "Both deployed." -ForegroundColor Green
Write-Host "  Site:  https://jamago.qa" -ForegroundColor Green
Write-Host "  API:   https://jamago.qa/api/Staff" -ForegroundColor Green
Write-Host "  Admin: https://jamago.qa/admin/login" -ForegroundColor Green
