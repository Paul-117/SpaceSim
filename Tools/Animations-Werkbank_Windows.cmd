@echo off
setlocal
set "ROOT=%~dp0.."
for %%I in ("%ROOT%") do set "ROOT=%%~fI"
call "%ROOT%\Animation-Workbench_Windows.cmd"
