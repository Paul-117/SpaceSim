@echo off
setlocal
set "server=%~1"
if not defined server set "server=127.0.0.1"
start "" "http://%server%:47870/armarium/"
