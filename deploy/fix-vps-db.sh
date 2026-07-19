#!/usr/bin/env bash
# Repair Postgres role/db for jamago-api, then restart and test the API.
set -eu

echo "==> ensure postgres running"
systemctl start postgresql 2>/dev/null || true
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
ALTER DATABASE jamago_db OWNER TO jamago_admin;
SQL

echo "==> ensure password auth from localhost"
PG_HBA=$(sudo -u postgres psql -tA -c "SHOW hba_file;" | tr -d '[:space:]')
if [ -n "$PG_HBA" ] && [ -f "$PG_HBA" ]; then
  if ! grep -Eq '^host\s+all\s+all\s+127\.0\.0\.1/32\s+(scram-sha-256|md5)' "$PG_HBA"; then
    echo "host all all 127.0.0.1/32 scram-sha-256" >> "$PG_HBA"
    echo "host all all ::1/128 scram-sha-256" >> "$PG_HBA"
    systemctl reload postgresql 2>/dev/null || true
    echo "pg_hba updated"
  else
    echo "pg_hba already allows localhost password auth"
  fi
fi

echo "==> test db login as jamago_admin"
PGPASSWORD='Jamago@123' psql -h 127.0.0.1 -U jamago_admin -d jamago_db \
  -c "SELECT current_user, current_database();"

echo "==> restart api"
chown www-data:www-data /var/www/jamago-api/appsettings.Production.json 2>/dev/null || true
chmod 640 /var/www/jamago-api/appsettings.Production.json 2>/dev/null || true
systemctl restart jamago-api
sleep 3
systemctl is-active jamago-api

echo "==> recent logs"
journalctl -u jamago-api -n 25 --no-pager || true

echo "==> test staff endpoint"
curl -sS -o /tmp/jama-staff.out -w "staff_http=%{http_code}\n" \
  -H "Host: jamago.qa" http://127.0.0.1:5093/api/staff || true
head -c 300 /tmp/jama-staff.out; echo

echo "==> test login endpoint"
curl -sS -o /tmp/jama-login.out -w "login_http=%{http_code}\n" \
  -H "Host: jamago.qa" -H "Content-Type: application/json" \
  -d '{"email":"admin@jamago.qa","password":"Admin@jamago2026!"}' \
  http://127.0.0.1:5093/api/auth/login || true
head -c 300 /tmp/jama-login.out; echo

echo "==> done"
