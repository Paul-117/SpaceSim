# Local Qwen intent recognition

SpaceSim optionally uses a local Qwen3 1.7B model after Whisper has transcribed a voice command.

## Security and authority

The model has no access to the WorldState and cannot call simulation code. It can return only this intent schema:

```json
{"intent":"autopilot","state":"on|off"}
{"intent":"reactor","percent":0}
{"intent":"sonar","state":"on|off"}
{"intent":"unknown"}
```

SpaceSim validates the JSON and converts valid responses back into the existing canonical command path. Invalid JSON, values outside `0`–`100`, and unknown intents are rejected.

## Execution order

1. Whisper transcribes the local microphone recording.
2. The fast deterministic parser handles direct phrasing such as `Autopilot an`.
3. Other phrases are sent to local Qwen, for example `Kannst du den Reaktor auf achtzig hochfahren?`.
4. Qwen returns a constrained intent.
5. The authoritative simulation validates and executes the corresponding command.

## One-time installation

Run from the repository root:

```powershell
./Tools/Voice/Install-QwenIntent_Windows.ps1
```

The script downloads the official Windows x64 CPU build of `llama.cpp` and the official Qwen3-1.7B Q8 GGUF model. The model is approximately 1.8 GB. Both files are local-only and excluded from Git.

At the first non-direct voice command, SpaceSim starts `llama-server.exe` on `127.0.0.1:47921`. It is stopped when the game closes. No browser station, LAN device, account or cloud service is involved.
