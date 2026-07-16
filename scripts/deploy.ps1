# Deploy Jama Go API to Hostinger VPS
# Usage: powershell -ExecutionPolicy Bypass -File scripts/deploy.ps1

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root "publish"
$remoteHost = "root@76.13.133.53"
$remoteApp = "/var/www/jamago-api"
$prodSettings = Join-Path $root "deploy\appsettings.Production.json"
$serviceFile = Join-Path $root "deploy\jamago-api.service"
$remoteSetup = Join-Path $root "deploy\remote-setup.sh"

Set-Location $root

if (-not (Test-Path $prodSettings)) {
  Write-Host "Missing $prodSettings" -ForegroundColor Red
  Write-Host "Copy the example and fill real secrets:" -ForegroundColor Yellow
  Write-Host "  copy deploy\appsettings.Production.json.example deploy\appsettings.Production.json"
  exit 1
}

Write-Host "Publishing Release build..." -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
dotnet publish ".\Jama.Web\Jama.Web.csproj" -c Release -o $publishDir --self-contained false
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Copy-Item $prodSettings (Join-Path $publishDir "appsettings.Production.json") -Force

Write-Host ""
Write-Host "Enter your VPS root password when prompted." -ForegroundColor Yellow

Write-Host "Preparing remote app directory..." -ForegroundColor Cyan
ssh -o StrictHostKeyChecking=accept-new $remoteHost "mkdir -p $remoteApp"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Stopping API service (if running)..." -ForegroundColor Cyan
ssh $remoteHost "systemctl stop jamago-api 2>/dev/null || true"

Write-Host "Uploading API files..." -ForegroundColor Cyan
scp -o StrictHostKeyChecking=accept-new -r "$publishDir\*" "${remoteHost}:${remoteApp}/"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

scp $serviceFile "${remoteHost}:/etc/systemd/system/jamago-api.service"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

scp $remoteSetup "${remoteHost}:/tmp/jamago-api-remote-setup.sh"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Running remote setup (dotnet runtime, systemd, nginx /api)..." -ForegroundColor Cyan
# Strip Windows CRLF so bash does not fail on "set -o pipefail"
ssh $remoteHost "sed -i 's/\r`$//' /tmp/jamago-api-remote-setup.sh && chmod +x /tmp/jamago-api-remote-setup.sh && bash /tmp/jamago-api-remote-setup.sh"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "API deployed." -ForegroundColor Green
Write-Host "  Health:  https://jamago.qa/api/Staff" -ForegroundColor Green
Write-Host "  Admin:   https://jamago.qa/admin/login  (after frontend deploy)" -ForegroundColor Green
