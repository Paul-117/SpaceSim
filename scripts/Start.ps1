param([switch]$Editor, [switch]$SkipBuild, [switch]$Duel)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$engine = Join-Path $projectRoot 'Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe'
$game = Join-Path $projectRoot 'SpaceSim.Godot'
if (-not (Test-Path -LiteralPath $engine)) { throw "Godot .NET not found at $engine" }

$importStamp = Join-Path $game '.godot/.spacesim-import-revision'
$gitRevision = ''
if (Get-Command git -ErrorAction SilentlyContinue) {
    $gitRevision = (git -C $projectRoot rev-parse HEAD 2>$null).Trim()
}

# Godot's generated import cache is intentionally not stored in Git. Rebuild it once
# after every checked-out revision so a fresh Windows clone cannot start with missing PNGs.
$needsImport = -not (Test-Path -LiteralPath (Join-Path $game '.godot/imported'))
if (-not $needsImport -and $gitRevision) {
    $needsImport = -not (Test-Path -LiteralPath $importStamp) -or
        (Get-Content -LiteralPath $importStamp -Raw).Trim() -ne $gitRevision
}
if ($needsImport) {
    Write-Host 'Importing Godot assets for this Git revision ...'
    & $engine --headless --editor --path $game --import --quit
    if ($LASTEXITCODE -ne 0) { throw 'Godot asset import failed.' }
    New-Item -ItemType Directory -Path (Split-Path -Parent $importStamp) -Force | Out-Null
    Set-Content -LiteralPath $importStamp -Value $gitRevision -NoNewline
}

if (-not $SkipBuild) { & (Join-Path $PSScriptRoot 'Build.ps1') }
if ($Editor) { & $engine --path $game --editor }
elseif ($Duel) { & $engine --path $game -- --duel }
else { & $engine --path $game }
if ($LASTEXITCODE -ne 0) { throw 'Godot exited with an error.' }
