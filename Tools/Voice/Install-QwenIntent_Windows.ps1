param(
    [string]$LlamaBuild = "b11415"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$voiceRoot = Join-Path $repositoryRoot "SpaceSim.Godot\Voice"
$runtimeDirectory = Join-Path $voiceRoot "IntentRuntime"
$modelsDirectory = Join-Path $voiceRoot "IntentModels"
$temporaryDirectory = Join-Path $env:TEMP "spacesim-qwen-intent-install"

New-Item -ItemType Directory -Force -Path $runtimeDirectory, $modelsDirectory, $temporaryDirectory | Out-Null
$archivePath = Join-Path $temporaryDirectory "llama-win-cpu-x64.zip"
$extractDirectory = Join-Path $temporaryDirectory "extract"

Write-Host "Downloading llama.cpp $LlamaBuild (Windows x64 CPU)…"
Invoke-WebRequest -Uri "https://github.com/ggml-org/llama.cpp/releases/download/$LlamaBuild/llama-$LlamaBuild-bin-win-cpu-x64.zip" -OutFile $archivePath
Remove-Item -Recurse -Force $extractDirectory -ErrorAction SilentlyContinue
Expand-Archive -Path $archivePath -DestinationPath $extractDirectory -Force
$server = Get-ChildItem -Path $extractDirectory -Filter "llama-server.exe" -File -Recurse | Select-Object -First 1
if ($null -eq $server) { throw "llama-server.exe was not found in the official archive." }
Copy-Item -Path (Join-Path $server.Directory.FullName "*") -Destination $runtimeDirectory -Recurse -Force

$modelPath = Join-Path $modelsDirectory "Qwen3-1.7B-Q8_0.gguf"
$temporaryModelPath = Join-Path $temporaryDirectory "Qwen3-1.7B-Q8_0.gguf"
Write-Host "Downloading Qwen3 1.7B Q8 model (about 1.8 GB)…"
Remove-Item -Force $temporaryModelPath -ErrorAction SilentlyContinue
& curl.exe --fail --location --output $temporaryModelPath "https://huggingface.co/Qwen/Qwen3-1.7B-GGUF/resolve/main/Qwen3-1.7B-Q8_0.gguf?download=true"
if ($LASTEXITCODE -ne 0 -or (Get-Item $temporaryModelPath).Length -lt 1GB) {
    Remove-Item -Force $temporaryModelPath -ErrorAction SilentlyContinue
    throw "The Qwen3 model download did not complete."
}
Copy-Item -LiteralPath $temporaryModelPath -Destination $modelPath -Force

Write-Host "Installed successfully. SpaceSim starts Qwen locally on demand at 127.0.0.1:47921."
