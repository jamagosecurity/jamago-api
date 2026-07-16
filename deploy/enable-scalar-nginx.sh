#!/usr/bin/env bash
# Add Scalar + OpenAPI nginx proxies (run on VPS once)
set -eu

SITE=""
for f in /etc/nginx/sites-enabled/* /etc/nginx/conf.d/*.conf; do
  [ -f "$f" ] || continue
  if grep -q 'jamago.qa' "$f" 2>/dev/null; then
    SITE="$f"
    break
  fi
done

if [ -z "$SITE" ]; then
  echo "No jamago.qa nginx site found"
  exit 1
fi

python3 - "$SITE" <<'PY'
import sys
from pathlib import Path

path = Path(sys.argv[1])
text = path.read_text()

blocks = []
for name in ("/scalar/", "/openapi/"):
    if f"location {name}" in text:
        print(f"already present: {name}")
        continue
    blocks.append(f"""
    location {name} {{
        proxy_pass         http://127.0.0.1:5093;
        proxy_http_version 1.1;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Real-IP         $remote_addr;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_set_header   Connection        "";
    }}
""")
    print(f"adding: {name}")

if blocks:
    insert = "\n".join(blocks) + "\n"
    marker = "location / {"
    idx = text.find(marker)
    if idx == -1:
        idx = text.rfind("}")
        text = text[:idx] + insert + text[idx:]
    else:
        text = text[:idx] + insert + text[idx:]
    path.write_text(text)
    print("Updated", path)
else:
    print("Nothing to change")
PY

nginx -t
systemctl reload nginx
echo "Done. Open https://jamago.qa/scalar/v1"
