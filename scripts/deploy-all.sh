#!/usr/bin/env bash
#
# Deploy the API and the frontend to the Hostinger VPS, from macOS.
#
#   bash scripts/deploy-all.sh
#
# The macOS counterpart of deploy-all.ps1. The .ps1 scripts are kept for a
# Windows machine; they cannot run here, and neither can the Node bootstrap
# that `npm run build` fires (it downloads a win-x64 Node and unzips it with
# PowerShell), which is why the Angular build calls ng directly below.
#
# ssh and scp each prompt for the VPS root password, so run this from a
# terminal you can type into. Nothing here is destructive locally.
#
set -euo pipefail

API_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WEB_ROOT="$(cd "$API_ROOT/../jama-go" && pwd)"

REMOTE_HOST="root@76.13.133.53"
REMOTE_APP="/var/www/jamago-api"
REMOTE_WEB="/var/www/jamago.qa"
DIST="$WEB_ROOT/dist/jamago-security/browser"

# dotnet is not on PATH on this machine; the SDK lives under ~/.dotnet.
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"

step() { printf '\n\033[36m==> %s\033[0m\n' "$1"; }

# ---------------------------------------------------------------------------
# 1/4  Build both, before touching the server
# ---------------------------------------------------------------------------
# Built first on purpose: a compile error should stop the deploy while the
# site is still up, not after the API has been taken down.

step "Publishing the API (Release)"
rm -rf "$API_ROOT/publish"
dotnet publish "$API_ROOT/Jama.Web/Jama.Web.csproj" \
  -c Release -o "$API_ROOT/publish" --self-contained false --nologo

# appsettings.Production.json holds the real secrets, is gitignored, and lives
# ONLY on the server. dotnet publish does not emit one, so uploading publish/
# leaves the server's copy untouched — which is what we want. It is copied in
# here only if someone keeps a local one deliberately.
if [ -f "$API_ROOT/deploy/appsettings.Production.json" ]; then
  cp "$API_ROOT/deploy/appsettings.Production.json" "$API_ROOT/publish/"
  echo "    local appsettings.Production.json will be uploaded"
else
  echo "    using the server's existing appsettings.Production.json"
fi

step "Building the frontend (production)"
# ng directly, never `npm run build` — see the header.
(cd "$WEB_ROOT" && ./node_modules/.bin/ng build --configuration production)

[ -f "$DIST/index.csr.html" ] || { echo "Build output missing at $DIST" >&2; exit 1; }

# ---------------------------------------------------------------------------
# 2/4  Back up, because starting the API also migrates the database
# ---------------------------------------------------------------------------
# ApplicationDbContextInitialiser calls MigrateAsync on boot, so a deploy is a
# schema migration whether or not this release adds one.

step "Backing up the database"
ssh -o StrictHostKeyChecking=accept-new "$REMOTE_HOST" '
  set -e
  mkdir -p /root/jamago-backups
  STAMP=$(date +%Y%m%d%H%M%S)
  sudo -u postgres pg_dump jamago_db | gzip > /root/jamago-backups/jamago_db.$STAMP.sql.gz
  echo "    saved /root/jamago-backups/jamago_db.$STAMP.sql.gz"
'

# ---------------------------------------------------------------------------
# 3/4  API
# ---------------------------------------------------------------------------

step "Deploying the API"
ssh "$REMOTE_HOST" "mkdir -p $REMOTE_APP && (systemctl stop jamago-api 2>/dev/null || true)"
scp -r "$API_ROOT/publish/." "$REMOTE_HOST:$REMOTE_APP/"
scp "$API_ROOT/deploy/jamago-api.service" "$REMOTE_HOST:/etc/systemd/system/jamago-api.service"
ssh "$REMOTE_HOST" "
  chown -R www-data:www-data $REMOTE_APP
  systemctl daemon-reload
  systemctl enable jamago-api >/dev/null 2>&1 || true
  systemctl start jamago-api
"

# The app needs several seconds to apply migrations before it listens. Checking
# sooner is what makes remote-setup.sh report a 502 on a perfectly good deploy.
step "Waiting for the API"
for i in $(seq 1 30); do
  code=$(ssh "$REMOTE_HOST" 'curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:5093/api/staff' || echo 000)
  # 401 is the healthy answer: the endpoint requires authentication.
  if [ "$code" = "401" ] || [ "$code" = "200" ]; then
    echo "    up (HTTP $code)"
    break
  fi
  [ "$i" = "30" ] && { echo "    API did not come up; check: journalctl -u jamago-api -n 50" >&2; exit 1; }
  sleep 2
done

# ---------------------------------------------------------------------------
# 4/4  Frontend
# ---------------------------------------------------------------------------

step "Deploying the frontend"
scp -r "$DIST/." "$REMOTE_HOST:$REMOTE_WEB/"
ssh "$REMOTE_HOST" "chown -R www-data:www-data $REMOTE_WEB && chmod -R 755 $REMOTE_WEB"

step "Checking the live site"
for path in / /admin /api/staff; do
  printf '    %-12s %s\n' "$path" "$(curl -s -o /dev/null -w '%{http_code}' "https://jamago.qa$path")"
done

# Every bundle the shell asks for must resolve. A single 404 here is a blank
# site: nothing runs, and with a cache-forever header on the error it can stick.
step "Checking every bundle resolves"
missing=0
for f in $(curl -s https://jamago.qa/index.csr.html \
           | grep -oE '(src|href)="(chunk|main|polyfills|styles)[^"]*"' \
           | sed 's/.*="//;s/"//' | sort -u); do
  code=$(curl -s -o /dev/null -w '%{http_code}' "https://jamago.qa/$f")
  [ "$code" = "200" ] || { echo "    MISSING $f -> $code" >&2; missing=1; }
done
[ "$missing" = "0" ] && echo "    all bundles 200"

printf '\n\033[32mBoth deployed.\033[0m\n'
echo "  Site:  https://jamago.qa"
echo "  Admin: https://jamago.qa/admin/login"
echo
echo "Hard-refresh once (Cmd+Shift+R) if a page looks stale."
