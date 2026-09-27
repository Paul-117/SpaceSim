@echo off
setlocal
set "server=%~1"
if not defined server (
  echo SpaceSim Armarium Browser
  echo Enter the LAN IP address of the main SpaceSim PC.
  set /p "server=Main PC IP (for example 192.168.178.20): "
)
if not defined server set "server=127.0.0.1"
start "" "http://%server%:47870/armarium/"
