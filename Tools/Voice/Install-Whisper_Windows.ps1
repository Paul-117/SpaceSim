param(
    # v1.9.4 is a source-only tag; its linked official build b5130 contains the Windows assets.
    [string]$WhisperVersion = "b5130"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$voiceRoot = Join-Path $repositoryRoot "SpaceSim.Godot\Voice"
$runtimeDirectory = Join-Path $voiceRoot "Runtime"
$modelsDirectory = Join-Path $voiceRoot "Models"
$temporaryDirectory = Join-Path $env:TEMP "spacesim-whisper-install"

New-Item -ItemType Directory -Force -Path $runtimeDirectory, $modelsDirectory, $temporaryDirectory | Out-Null
$archivePath = Join-Path $temporaryDirectory "whisper-bin-x64.zip"
$extractDirectory = Join-Path $temporaryDirectory "extract"

Write-Host "Downloading whisper.cpp $WhisperVersion (Windows x64)…"
Invoke-WebRequest -Uri "https://github.com/ggml-org/whisper.cpp/releases/download/$WhisperVersion/whisper-bin-x64.zip" -OutFile $archivePath
Remove-Item -Recurse -Force $extractDirectory -ErrorAction SilentlyContinue
Expand-Archive -Path $archivePath -DestinationPath $extractDirectory -Force
$cli = Get-ChildItem -Path $extractDirectory -Filter "whisper-cli.exe" -File -Recurse | Select-Object -First 1
if ($null -eq $cli) { throw "whisper-cli.exe was not found in the official archive." }
Copy-Item -Path (Join-Path $cli.Directory.FullName "*") -Destination $runtimeDirectory -Recurse -Force

$modelPath = Join-Path $modelsDirectory "ggml-base.bin"
$temporaryModelPath = Join-Path $temporaryDirectory "ggml-base.bin"
Write-Host "Downloading the German-capable Whisper base model…"
Remove-Item -Force $temporaryModelPath -ErrorAction SilentlyContinue
& curl.exe --fail --location --output $temporaryModelPath "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin?download=true"
if ($LASTEXITCODE -ne 0 -or (Get-Item $temporaryModelPath).Length -lt 100MB) {
    Remove-Item -Force $temporaryModelPath -ErrorAction SilentlyContinue
    throw "The Whisper model download did not complete."
}
Copy-Item -LiteralPath $temporaryModelPath -Destination $modelPath -Force

Write-Host "Installed successfully. Start SpaceSim, then hold V to speak a command."
