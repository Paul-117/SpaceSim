$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$env:DOTNET_CLI_HOME = Join-Path $root '.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
& dotnet run --project (Join-Path $PSScriptRoot 'GefechtsSimulation.csproj') --configuration Debug
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
