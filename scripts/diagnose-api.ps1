# Quick VPS API/DB diagnosis
# Usage: powershell -ExecutionPolicy Bypass -File scripts/diagnose-api.ps1

$ErrorActionPreference = "Stop"
$remoteHost = "root@76.13.133.53"

Write-Host "Enter VPS root password when prompted." -ForegroundColor Yellow

$remoteCmd = @'
set -e
echo "==> service"
systemctl is-active jamago-api || true
echo "==> last logs"
journalctl -u jamago-api -n 40 --no-pager || true
echo "==> local staff"
curl -sS -w "\nHTTP %{http_code}\n" -H "Host: jamago.qa" http://127.0.0.1:5093/api/staff | head -c 400; echo
echo "==> postgres users/db"
sudo -u postgres psql -c "\du" || true
sudo -u postgres psql -c "\l" || true
echo "==> test jamago_admin login"
sudo -u postgres psql -d jamago_db -c "SELECT COUNT(*) AS admin_users FROM \"AdminUsers\";" || true
sudo -u postgres psql -d jamago_db -c "SELECT COUNT(*) AS staff_rows FROM \"Staff\";" || true
'@

ssh $remoteHost $remoteCmd
