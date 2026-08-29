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
  # sites-enabled entries are usually symlinks; edit and back up the real file.
  SITE="$(readlink -f "$SITE")"

  # The block may already be there as a plain prefix or with the ^~ modifier.
  # Matching only the literal "location /api/" misses "location ^~ /api/" and
  # appends a second block, which nginx rejects as a duplicate location.
  if grep -qE 'location[[:space:]]+(\^~[[:space:]]*)?/api/' "$SITE"; then
    echo "nginx /api/ location already present in $SITE"
  else
    echo "Inserting /api/ proxy into $SITE"
    BACKUP="$SITE.$(date +%Y%m%d%H%M%S).bak"
    cp -a "$SITE" "$BACKUP"
    echo "Backed up to $BACKUP"

    python3 - "$SITE" <<'PY'
import re
import sys
from pathlib import Path

path = Path(sys.argv[1])
text = path.read_text()

# ^~ so the regex asset locations further down cannot capture a proxied URL
# that happens to end in .js or .css.
block = """    location ^~ /api/ {
        proxy_pass         http://127.0.0.1:5093;
        proxy_http_version 1.1;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Real-IP         $remote_addr;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_set_header   Connection        "";
    }

"""

# Anchor to the start of the `location / {` line, not the match offset, or the
# insert lands mid-line and strips that line's indentation.
match = re.search(r'^[ \t]*location\s+/\s*\{', text, re.MULTILINE)
idx = match.start() if match else text.rfind("}")
path.write_text(text[:idx] + block + text[idx:])
print("Updated", path)
PY

    # Roll back rather than leave a config that breaks the next reload — nginx
    # keeps serving the old one until then, so a bad file hides until reboot.
    if ! nginx -t; then
      echo "nginx -t failed; restoring $BACKUP" >&2
      cp -a "$BACKUP" "$SITE"
      nginx -t
      exit 1
    fi
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
