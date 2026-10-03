#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
editor_file="$script_dir/index.html"

if [[ ! -f "$editor_file" ]]; then
  printf 'Bridge Layout Editor wurde nicht gefunden: %s\n' "$editor_file" >&2
  exit 1
fi

for browser in firefox firefox-esr google-chrome chromium chromium-browser; do
  if command -v "$browser" >/dev/null 2>&1; then
    exec "$browser" --new-window "$editor_file"
  fi
done

printf 'Kein unterstuetzter Browser gefunden. Bitte Firefox installieren.\n' >&2
exit 1
