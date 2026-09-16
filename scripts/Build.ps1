param([ValidateSet('Debug', 'ExportDebug', 'ExportRelease')][string]$Configuration = 'Debug')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $projectRoot
try {
    $env:DOTNET_CLI_HOME = Join-Path $projectRoot '.dotnet'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    & dotnet build SpaceSim.sln --configuration $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
} finally {
    Pop-Location
}
