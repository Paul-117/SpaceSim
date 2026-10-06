@echo off
setlocal
set "ROOT=%~dp0.."
for %%I in ("%ROOT%") do set "ROOT=%%~fI"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ROOT%\Tools\GefechtsSimulation\Start-GefechtsSimulation.ps1"
if errorlevel 1 pause
