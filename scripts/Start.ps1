param([switch]$Editor, [switch]$SkipBuild, [switch]$Duel)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $SkipBuild) { & (Join-Path $PSScriptRoot 'Build.ps1') }
$engine = Join-Path $projectRoot 'Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe'
$game = Join-Path $projectRoot 'SpaceSim.Godot'
if (-not (Test-Path -LiteralPath $engine)) { throw "Godot .NET not found at $engine" }
if ($Editor) { & $engine --path $game --editor }
elseif ($Duel) { & $engine --path $game -- --duel }
else { & $engine --path $game }
if ($LASTEXITCODE -ne 0) { throw 'Godot exited with an error.' }
