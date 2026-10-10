param(
    [string]$PiperVersion = "2023.11.14-2"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$voiceRoot = Join-Path $repositoryRoot "SpaceSim.Godot\Voice"
$runtimeDirectory = Join-Path $voiceRoot "PiperRuntime"
$modelsDirectory = Join-Path $voiceRoot "PiperModels"
$temporaryDirectory = Join-Path $env:TEMP "spacesim-piper-install"

New-Item -ItemType Directory -Force -Path $runtimeDirectory, $modelsDirectory, $temporaryDirectory | Out-Null
$archivePath = Join-Path $temporaryDirectory "piper_windows_amd64.zip"
$extractDirectory = Join-Path $temporaryDirectory "extract"

Write-Host "Downloading Piper $PiperVersion (Windows x64)…"
Invoke-WebRequest -Uri "https://github.com/rhasspy/piper/releases/download/$PiperVersion/piper_windows_amd64.zip" -OutFile $archivePath
Remove-Item -Recurse -Force $extractDirectory -ErrorAction SilentlyContinue
Expand-Archive -Path $archivePath -DestinationPath $extractDirectory -Force
$piper = Get-ChildItem -Path $extractDirectory -Filter "piper.exe" -File -Recurse | Select-Object -First 1
if ($null -eq $piper) { throw "piper.exe was not found in the official archive." }
Copy-Item -Path (Join-Path $piper.Directory.FullName "*") -Destination $runtimeDirectory -Recurse -Force

$modelPath = Join-Path $modelsDirectory "en_US-amy-medium.onnx"
$configPath = "$modelPath.json"
Write-Host "Downloading Piper voice en_US-amy-medium (about 63 MB)…"
& curl.exe --fail --location --output $modelPath "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/amy/medium/en_US-amy-medium.onnx?download=true"
if ($LASTEXITCODE -ne 0 -or (Get-Item $modelPath).Length -lt 50MB) { throw "The Amy medium model download did not complete." }
& curl.exe --fail --location --output $configPath "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/amy/medium/en_US-amy-medium.onnx.json?download=true"
if ($LASTEXITCODE -ne 0 -or (Get-Item $configPath).Length -lt 1KB) { throw "The Amy medium configuration download did not complete." }

Write-Host "Installed successfully. SpaceSim now speaks Bridge notifications locally through Piper Amy medium."
