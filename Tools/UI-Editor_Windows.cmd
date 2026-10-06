@echo off
setlocal
set "ROOT=%~dp0.."
for %%I in ("%ROOT%") do set "ROOT=%%~fI"
call "%ROOT%\StationPrototypes\BridgeLayoutEditor\Start_BridgeLayoutEditor_Windows.cmd"
