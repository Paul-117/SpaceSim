param([switch]$SkipBuild, [switch]$GodotSmoke)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $SkipBuild) { & (Join-Path $PSScriptRoot 'Build.ps1') }
& dotnet (Join-Path $projectRoot 'SpaceSim.Core.Tests/bin/Debug/net10.0/SpaceSim.Core.Tests.dll')
if ($LASTEXITCODE -ne 0) { throw 'Core tests failed.' }
if ($GodotSmoke) {
    $engine = Join-Path $projectRoot 'Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe'
    $game = Join-Path $projectRoot 'SpaceSim.Godot'
    $artifacts = Join-Path $projectRoot '.artifacts'
    New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
    & $engine --headless --path $game --editor --import --quit --log-file (Join-Path $artifacts 'import.log')
    if ($LASTEXITCODE -ne 0) { throw 'Godot import failed.' }
    & $engine --headless --path $game --log-file (Join-Path $artifacts 'smoke.log') -- --smoke-test
    if ($LASTEXITCODE -ne 0) { throw 'Godot smoke test failed.' }
    if (-not (Select-String -LiteralPath (Join-Path $artifacts 'smoke.log') -SimpleMatch 'SMOKE PASS' -Quiet)) {
        throw 'Godot did not complete the smoke test.'
    }
    & $engine --headless --path $game --log-file (Join-Path $artifacts 'warp-smoke.log') -- --warp-smoke-test
    if ($LASTEXITCODE -ne 0) { throw 'Godot warp smoke test failed.' }
    if (-not (Select-String -LiteralPath (Join-Path $artifacts 'warp-smoke.log') -SimpleMatch 'WARP SMOKE PASS' -Quiet)) {
        throw 'Godot did not complete the warp smoke test.'
    }
    foreach ($log in @('import.log', 'smoke.log', 'warp-smoke.log')) {
        if (Select-String -LiteralPath (Join-Path $artifacts $log) -Pattern 'ERROR:|SCRIPT ERROR:|Unhandled exception' -Quiet) {
            throw "Godot errors in $log"
        }
    }
}
