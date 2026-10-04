#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
editor_file="$script_dir/index.html"
repo_root="$(cd -- "$script_dir/../.." && pwd)"
port=47871

if [[ ! -f "$editor_file" ]]; then
  printf 'Bridge Layout Editor wurde nicht gefunden: %s\n' "$editor_file" >&2
  exit 1
fi

if ! curl --silent --fail "http://127.0.0.1:$port/api/bridge-assets" >/dev/null 2>&1; then
  python3 "$script_dir/bridge_layout_server.py" --root "$repo_root" --port "$port" >/tmp/spacesim-bridge-layout-editor.log 2>&1 &
  sleep 1
fi

editor_url="http://127.0.0.1:$port/StationPrototypes/BridgeLayoutEditor/"
for browser in firefox firefox-esr google-chrome chromium chromium-browser; do
  if command -v "$browser" >/dev/null 2>&1; then
    exec "$browser" --new-window "$editor_url"
  fi
done

printf 'Kein unterstuetzter Browser gefunden. Bitte Firefox installieren.\n' >&2
exit 1
