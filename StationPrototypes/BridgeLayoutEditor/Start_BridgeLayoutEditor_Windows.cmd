@echo off
setlocal
set "ROOT=%~dp0..\.."
for %%I in ("%ROOT%") do set "ROOT=%%~fI"
set "PORT=47871"

powershell -NoProfile -WindowStyle Hidden -Command "$open = Test-NetConnection 127.0.0.1 -Port %PORT% -InformationLevel Quiet -WarningAction SilentlyContinue; if (-not $open) { Start-Process -WindowStyle Hidden -FilePath python -ArgumentList @('%~dp0bridge_layout_server.py','--root','%ROOT%','--port','%PORT%') }"
timeout /t 1 /nobreak >nul
start "SpaceSim Bridge Layout Editor" "http://127.0.0.1:%PORT%/StationPrototypes/BridgeLayoutEditor/"
