#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GODOT="${GODOT_BIN:-}"
if [[ -z "$GODOT" ]]; then
  GODOT="$(command -v godot4 || command -v godot || true)"
fi
if [[ -z "$GODOT" ]]; then
  echo "Godot 4 wurde nicht gefunden. Setze GODOT_BIN auf den Pfad zur Godot-4-Executable."
  exit 1
fi
exec "$GODOT" --path "$ROOT/SpaceSim.Godot" --scene res://Scenes/AnimationWorkbench.tscn
