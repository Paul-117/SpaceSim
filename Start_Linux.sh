#!/usr/bin/env bash

set -euo pipefail

project_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
godot_project="$project_root/SpaceSim.Godot"

fail() {
    printf 'Fehler: %s\n' "$1" >&2
    exit 1
}

if ! command -v dotnet >/dev/null 2>&1; then
    fail "Das .NET SDK 10 fehlt. Installiere es mit: sudo apt install dotnet-sdk-10.0"
fi

if ! dotnet --list-sdks | grep -q '^10\.'; then
    fail "Das Projekt benoetigt das .NET SDK 10. Installiere es mit: sudo apt install dotnet-sdk-10.0"
fi

godot_bin="${GODOT_BIN:-}"

if [[ -z "$godot_bin" ]]; then
    for command_name in godot4-mono godot-mono godot4 godot; do
        if command -v "$command_name" >/dev/null 2>&1; then
            godot_bin="$(command -v "$command_name")"
            break
        fi
    done
fi

if [[ -z "$godot_bin" ]]; then
    for search_root in "$project_root" "$(dirname -- "$project_root")"; do
        while IFS= read -r candidate; do
            godot_bin="$candidate"
            break 2
        done < <(
            find "$search_root" -maxdepth 3 -type f -executable \
                -name 'Godot_v4.7.2-stable_mono_linux*' -print
        )
    done
fi

if [[ -z "$godot_bin" || ! -x "$godot_bin" ]]; then
    fail "Godot 4.7.2 .NET fuer Linux wurde nicht gefunden. Entpacke es in den Projektordner oder setze GODOT_BIN auf die Godot-Datei."
fi

godot_dir="$(cd -- "$(dirname -- "$godot_bin")" && pwd)"
godot_bin="$godot_dir/$(basename -- "$godot_bin")"
package_source="$godot_dir/GodotSharp/Tools/nupkgs"

if [[ ! -d "$package_source" ]]; then
    fail "Die gefundene Godot-Version enthaelt keine .NET-Pakete. Lade Godot 4.7.2 .NET herunter, nicht die Standardversion."
fi

if ! find "$package_source" -maxdepth 1 -type f \
    -iname 'Godot.NET.Sdk.4.7.2*.nupkg' -print -quit | grep -q .; then
    fail "Die Godot-.NET-Pakete passen nicht zu Version 4.7.2."
fi

export DOTNET_CLI_HOME="$project_root/.dotnet"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export GODOT_NUGET_SOURCE="$package_source"

printf 'Baue SpaceSim mit %s ...\n' "$(basename -- "$godot_bin")"
dotnet restore "$project_root/SpaceSim.sln" \
    --source "$package_source" \
    --maxcpucount:1 \
    --nologo
dotnet build "$project_root/SpaceSim.sln" \
    --configuration Debug \
    --no-restore \
    --maxcpucount:1 \
    --nologo

printf 'Starte SpaceSim ...\n'
exec "$godot_bin" --path "$godot_project" "$@"
