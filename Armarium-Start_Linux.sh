#!/usr/bin/env sh
set -eu

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
server="${1:-}"
if [ -z "$server" ]; then
  host_file="$script_dir/Armarium-Host.txt"
  if [ ! -r "$host_file" ]; then
    printf 'Armarium host file is missing. Run Armarium-SyncHost_Windows.cmd on the main PC, then git pull here.\n' >&2
    exit 1
  fi
  server=$(tr -d '\r\n' < "$host_file")
  if [ -z "$server" ]; then
    printf 'Armarium host file is empty. Run Armarium-SyncHost_Windows.cmd on the main PC, then git pull here.\n' >&2
    exit 1
  fi
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
