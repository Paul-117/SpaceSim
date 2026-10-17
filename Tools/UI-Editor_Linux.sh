#!/usr/bin/env bash

set -euo pipefail

root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
exec "$root/StationPrototypes/BridgeLayoutEditor/Start_BridgeLayoutEditor_Linux.sh" Loadout
