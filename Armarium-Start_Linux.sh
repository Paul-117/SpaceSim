#!/usr/bin/env sh
set -eu

server="${1:-}"
if [ -z "$server" ]; then
  printf '%s' 'Main PC IP (for example 192.168.178.20): '
  IFS= read -r server
fi

if [ -z "$server" ]; then
  server="127.0.0.1"
fi

url="http://${server}:47870/armarium/"
printf 'Opening %s\n' "$url"

if command -v xdg-open >/dev/null 2>&1; then
  xdg-open "$url" >/dev/null 2>&1 &
elif command -v gio >/dev/null 2>&1; then
  gio open "$url" >/dev/null 2>&1 &
elif command -v firefox >/dev/null 2>&1; then
  firefox "$url" >/dev/null 2>&1 &
else
  printf 'No browser launcher found. Open this URL manually:\n%s\n' "$url" >&2
  exit 1
fi
