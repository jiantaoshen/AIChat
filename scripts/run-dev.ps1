# This file launches the Windows development environment, starts Ollama without blocking, verifies the configured model when available, then starts backend and frontend.
param(
    [string]$Model = "qwen3:4b-instruct-2507-q4_K_M"
)

$ErrorActionPreference = "Stop"

function Require-Command {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$InstallHint
    )

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        Write-Host "Missing dependency: $Name" -ForegroundColor Red
        Write-Host $InstallHint -ForegroundColor Yellow
        exit 1
    }
}

function Test-Url {
    param([string]$Url)

    try {
        Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 2 | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

function Start-OllamaWindowsApp {
    $ollamaCommand = Get-Command "ollama" -ErrorAction SilentlyContinue
    if ($null -eq $ollamaCommand) {
        return $false
    }

    $ollamaExe = $ollamaCommand.Source
    $ollamaDir = Split-Path -Parent $ollamaExe

    $appCandidates = @(
        (Join-Path $ollamaDir "ollama app.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs\Ollama\ollama app.exe"),
        (Join-Path $env:LOCALAPPDATA "Ollama\ollama app.exe"),
        (Join-Path $env:ProgramFiles "Ollama\ollama app.exe")
    ) | Select-Object -Unique

    foreach ($appExe in $appCandidates) {
        if (-not [string]::IsNullOrWhiteSpace($appExe) -and (Test-Path $appExe)) {
            Start-Process -FilePath $appExe -ArgumentList @("--hide", "--fast-startup") -WindowStyle Hidden | Out-Null
            return $true
        }
    }

    # Fallback for installations that only expose ollama.exe.
    Start-Process -FilePath $ollamaExe -ArgumentList @("serve") -WindowStyle Hidden | Out-Null
    return $true
}

function Ensure-OllamaApp {
    if (Test-Url "http://localhost:11434/api/tags") {
        return $true
    }

    Write-Host "Starting Ollama Windows app..." -ForegroundColor Cyan

    try {
        if (-not (Start-OllamaWindowsApp)) {
            return $false
        }
    }
    catch {
        Write-Host "Could not start Ollama automatically: $($_.Exception.Message)" -ForegroundColor Yellow
        return $false
    }

    # Never block startup indefinitely. Give Ollama a short window, then continue.
    for ($attempt = 0; $attempt -lt 15; $attempt++) {
        if (Test-Url "http://localhost:11434/api/tags") {
            return $true
        }
        Start-Sleep -Seconds 1
    }

    return $false
}

Require-Command "dotnet" "Install the latest stable .NET 10 SDK, then run SETUP_WINDOWS.cmd."
Require-Command "node" "Install Node.js 24 LTS or newer, then run SETUP_WINDOWS.cmd."
Require-Command "npm" "npm is normally installed together with Node.js."
Require-Command "ollama" "Install the current stable Ollama for Windows, then run SETUP_WINDOWS.cmd."

$projectRoot = Split-Path -Parent $PSScriptRoot
$backendDir = Join-Path $projectRoot "backend"
$frontendDir = Join-Path $projectRoot "frontend"
$frontendEnv = Join-Path $frontendDir ".env.local"
$frontendEnvExample = Join-Path $frontendDir ".env.local.example"
$frontendLock = Join-Path $frontendDir "package-lock.json"

$ollamaReady = Ensure-OllamaApp

if ($ollamaReady) {
    $tags = Invoke-RestMethod -Uri "http://localhost:11434/api/tags" -TimeoutSec 3
    $modelInstalled = @($tags.models | ForEach-Object { $_.name }) -contains $Model

    if (-not $modelInstalled) {
        Write-Host "$Model is missing. Pulling it now..." -ForegroundColor Yellow
        & ollama pull $Model
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Failed to pull $Model. Backend/frontend will still start, but chat will not work until the model is installed." -ForegroundColor Yellow
        }
    }
}
else {
    Write-Host "Ollama is not reachable yet. Backend/frontend will still start." -ForegroundColor Yellow
    Write-Host "If chat is unavailable, open Ollama manually and refresh the page." -ForegroundColor Yellow
}

if (-not (Test-Path $frontendEnv)) {
    Copy-Item $frontendEnvExample $frontendEnv
}

if (-not (Test-Path (Join-Path $frontendDir "node_modules"))) {
    Push-Location $frontendDir
    try {
        if (Test-Path $frontendLock) {
            Write-Host "Frontend dependencies are missing. Installing exactly from package-lock.json with npm ci..." -ForegroundColor Yellow
            & npm ci
            if ($LASTEXITCODE -ne 0) { throw "npm ci failed." }
        }
        else {
            Write-Host "package-lock.json is missing. Falling back to npm install..." -ForegroundColor Yellow
            & npm install
            if ($LASTEXITCODE -ne 0) { throw "npm install failed." }
        }
    }
    finally {
        Pop-Location
    }
}

Start-Process -FilePath "powershell.exe" -WorkingDirectory $backendDir -ArgumentList @(
    "-NoExit",
    "-NoProfile",
    "-Command",
    "dotnet run"
) | Out-Null

Start-Process -FilePath "powershell.exe" -WorkingDirectory $frontendDir -ArgumentList @(
    "-NoExit",
    "-NoProfile",
    "-Command",
    "npm run dev"
) | Out-Null

Write-Host ""
Write-Host "Starting local Qwen avatar..." -ForegroundColor Green
Write-Host "Ollama:   http://localhost:11434"
Write-Host "Backend:  http://localhost:5191"
Write-Host "Frontend: http://localhost:3000"
Write-Host "Model:    $Model"

for ($attempt = 0; $attempt -lt 60; $attempt++) {
    Start-Sleep -Seconds 1
    if (Test-Url "http://localhost:3000") {
        Start-Process "http://localhost:3000"
        exit 0
    }
}

Write-Host "Frontend did not become reachable automatically. Check the two console windows, then open http://localhost:3000 manually." -ForegroundColor Yellow
