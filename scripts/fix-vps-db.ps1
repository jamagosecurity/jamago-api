# Repair VPS Postgres + redeploy API production settings, then restart jamago-api.
# Usage: powershell -ExecutionPolicy Bypass -File scripts/fix-vps-db.ps1

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$prodSettings = Join-Path $root "deploy\appsettings.Production.json"
$remoteHost = "root@76.13.133.53"
$remoteApp = "/var/www/jamago-api"

if (-not (Test-Path $prodSettings)) {
  Write-Error "Missing $prodSettings"
}

Write-Host "This will:" -ForegroundColor Cyan
Write-Host "  1) Ensure Postgres role/db jamago_admin / jamago_db with password Jamago@123"
Write-Host "  2) Upload local appsettings.Production.json"
Write-Host "  3) Restart jamago-api and test /api/auth/login"
Write-Host ""
Write-Host "Enter VPS root password when prompted." -ForegroundColor Yellow

$remoteSql = @'
set -e
echo "==> start postgres"
systemctl start postgresql || systemctl start postgresql@* || true
systemctl is-active postgresql || true

echo "==> ensure role + database"
sudo -u postgres psql -v ON_ERROR_STOP=1 <<'SQL'
DO $$
BEGIN
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'jamago_admin') THEN
    CREATE ROLE jamago_admin LOGIN PASSWORD 'Jamago@123';
  ELSE
    ALTER ROLE jamago_admin WITH LOGIN PASSWORD 'Jamago@123';
  END IF;
END
$$;

SELECT 'CREATE DATABASE jamago_db OWNER jamago_admin'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'jamago_db')\gexec

GRANT ALL PRIVILEGES ON DATABASE jamago_db TO jamago_admin;
SQL

# Allow password auth from localhost if needed
PG_HBA=$(sudo -u postgres psql -tA -c "SHOW hba_file;" | tr -d '[:space:]')
if [ -n "$PG_HBA" ] && [ -f "$PG_HBA" ]; then
  if ! grep -Eq "^host\s+all\s+all\s+127.0.0.1/32\s+scram-sha-256|^host\s+all\s+all\s+127.0.0.1/32\s+md5" "$PG_HBA"; then
    echo "host all all 127.0.0.1/32 scram-sha-256" >> "$PG_HBA"
    echo "host all all ::1/128 scram-sha-256" >> "$PG_HBA"
    systemctl reload postgresql || systemctl reload postgresql@* || true
  fi
fi

echo "==> test db login as jamago_admin"
PGPASSWORD='Jamago@123' psql -h 127.0.0.1 -U jamago_admin -d jamago_db -c "SELECT current_user, current_database();"
'@

ssh $remoteHost $remoteSql
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Uploading Production settings..." -ForegroundColor Cyan
scp $prodSettings "${remoteHost}:${remoteApp}/appsettings.Production.json"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$remoteRestart = @'
set -e
chown www-data:www-data /var/www/jamago-api/appsettings.Production.json
chmod 640 /var/www/jamago-api/appsettings.Production.json
systemctl restart jamago-api
sleep 3
systemctl --no-pager --full status jamago-api || true
echo "==> journal"
journalctl -u jamago-api -n 30 --no-pager || true
echo "==> staff"
curl -sS -w "\nstaff_http=%{http_code}\n" -H "Host: jamago.qa" http://127.0.0.1:5093/api/staff | head -c 300; echo
echo "==> login"
curl -sS -w "\nlogin_http=%{http_code}\n" -H "Host: jamago.qa" -H "Content-Type: application/json" \
  -d '{"email":"admin@jamago.qa","password":"Admin@jamago2026!"}' \
  http://127.0.0.1:5093/api/auth/login | head -c 500; echo
'@

ssh $remoteHost $remoteRestart
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "Done. Try login again at https://jamago.qa/admin/login" -ForegroundColor Green
Write-Host "  Email: admin@jamago.qa" -ForegroundColor Green
Write-Host "  Password: Admin@jamago2026!" -ForegroundColor Green
