#!/usr/bin/env bash
set -euo pipefail

APP_DIR=/var/www/jamago-api

echo "==> Ensuring ASP.NET Core 10 runtime"
if ! command -v dotnet >/dev/null 2>&1 || ! dotnet --list-runtimes 2>/dev/null | grep -q 'Microsoft.AspNetCore.App 10.'; then
  wget -q https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
  chmod +x /tmp/dotnet-install.sh
  /tmp/dotnet-install.sh --channel 10.0 --runtime aspnetcore --install-dir /usr/share/dotnet
  ln -sfn /usr/share/dotnet/dotnet /usr/bin/dotnet
fi
dotnet --list-runtimes

echo "==> Permissions"
chown -R www-data:www-data "$APP_DIR"
chmod -R 755 "$APP_DIR"

echo "==> systemd"
systemctl daemon-reload
systemctl enable jamago-api
systemctl restart jamago-api
sleep 2
systemctl --no-pager --full status jamago-api || true

echo "==> nginx /api proxy"
SITE=""
for f in /etc/nginx/sites-enabled/* /etc/nginx/conf.d/*.conf; do
  [ -f "$f" ] || continue
  if grep -q 'jamago.qa' "$f" 2>/dev/null; then
    SITE="$f"
    break
  fi
done

if [ -z "$SITE" ]; then
  echo "WARNING: No nginx site containing jamago.qa found."
  echo "Add /api/ proxy manually from deploy/nginx-jamago.qa.conf.snippet"
else
  if grep -q 'location /api/' "$SITE"; then
    echo "nginx /api/ location already present in $SITE"
  else
    echo "Inserting /api/ proxy into $SITE"
    python3 - "$SITE" <<'PY'
import sys
from pathlib import Path

path = Path(sys.argv[1])
text = path.read_text()
block = """
    location /api/ {
        proxy_pass         http://127.0.0.1:5093;
        proxy_http_version 1.1;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Real-IP         $remote_addr;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_set_header   Connection        "";
    }
"""
marker = "location / {"
idx = text.find(marker)
if idx == -1:
    idx = text.rfind("}")
    text = text[:idx] + block + "\n" + text[idx:]
else:
    text = text[:idx] + block + "\n" + text[idx:]
path.write_text(text)
print("Updated", path)
PY
  fi
  nginx -t
  systemctl reload nginx
fi

echo "==> health checks"
curl -sS -o /tmp/jamago-local-staff.json -w "local_api=%{http_code}\n" -H "Host: jamago.qa" http://127.0.0.1:5093/api/staff || true
curl -sS -o /tmp/jamago-public-staff.json -w "public_api=%{http_code}\n" https://jamago.qa/api/staff || true
head -c 200 /tmp/jamago-local-staff.json 2>/dev/null; echo
head -c 200 /tmp/jamago-public-staff.json 2>/dev/null; echo

echo "Done."
