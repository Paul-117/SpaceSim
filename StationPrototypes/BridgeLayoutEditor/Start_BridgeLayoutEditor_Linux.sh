#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
editor_file="$script_dir/index.html"
repo_root="$(cd -- "$script_dir/../.." && pwd)"
ui_folder="${1:-}"
port=47871

# A named UI gets its own local server while an older generic editor might still
# be open in another terminal. This avoids silently serving stale JavaScript.
if [[ -n "$ui_folder" ]]; then
  port=47872
fi

if [[ ! -f "$editor_file" ]]; then
  printf 'Bridge Layout Editor wurde nicht gefunden: %s\n' "$editor_file" >&2
  exit 1
fi

if ! command -v python3 >/dev/null 2>&1; then
  printf 'Python 3 wurde nicht gefunden. Installiere es mit: sudo apt install python3\n' >&2
  exit 1
fi

if [[ -n "$ui_folder" && ! "$ui_folder" =~ ^[A-Za-z0-9_-]+$ ]]; then
  printf 'Ungueltiger UI-Ordnername: %s\n' "$ui_folder" >&2
  exit 1
fi

editor_url="http://127.0.0.1:$port/StationPrototypes/BridgeLayoutEditor/"
if [[ -n "$ui_folder" ]]; then
  editor_url+="?ui=$ui_folder"
fi
if curl --silent --fail "$editor_url" >/dev/null 2>&1; then
  if command -v xdg-open >/dev/null 2>&1; then
    xdg-open "$editor_url" >/dev/null 2>&1 &
  else
    printf 'Oeffne diese Adresse manuell:\n%s\n' "$editor_url"
  fi
  exit 0
fi

printf 'Starte Bridge Layout Editor auf Port %s ...\n' "$port"
printf 'Dieses Terminal muss waehrend der Editor geoeffnet ist weiterlaufen.\n'
server_args=(--root "$repo_root" --port "$port" --open)
if [[ -n "$ui_folder" ]]; then
  server_args+=(--ui "$ui_folder")
fi
exec python3 "$script_dir/bridge_layout_server.py" "${server_args[@]}"
