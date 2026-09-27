@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\SyncArmariumHost.ps1"
if errorlevel 1 pause
