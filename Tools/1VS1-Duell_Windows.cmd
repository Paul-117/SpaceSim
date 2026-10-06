@echo off
setlocal
set "ROOT=%~dp0.."
for %%I in ("%ROOT%") do set "ROOT=%%~fI"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ROOT%\scripts\Start.ps1" -Duel
if errorlevel 1 pause
