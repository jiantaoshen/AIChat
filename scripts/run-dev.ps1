# This file is the Windows 11 development launcher: it verifies the modern toolchain and Qwen model, starts backend/frontend, then opens the browser.
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

Require-Command "dotnet" "Install the latest stable .NET 10 SDK, then run SETUP_WINDOWS.cmd."
Require-Command "node" "Install Node.js 24 LTS or newer, then run SETUP_WINDOWS.cmd."
Require-Command "npm" "npm is normally installed together with Node.js."
Require-Command "ollama" "Install the current stable Ollama for Windows, then run SETUP_WINDOWS.cmd."

if (-not (Test-Url "http://localhost:11434/api/tags")) {
    Write-Host "Starting Ollama..." -ForegroundColor Cyan
    Start-Process -FilePath "ollama" -ArgumentList "serve" -WindowStyle Minimized | Out-Null

    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        Start-Sleep -Seconds 1
        if (Test-Url "http://localhost:11434/api/tags") {
            break
        }
    }
}

if (-not (Test-Url "http://localhost:11434/api/tags")) {
    Write-Host "Ollama is not reachable at http://localhost:11434." -ForegroundColor Red
    Write-Host "Open Ollama manually, then rerun this script."
    exit 1
}

$tags = Invoke-RestMethod -Uri "http://localhost:11434/api/tags"
$modelInstalled = @($tags.models | ForEach-Object { $_.name }) -contains $Model

if (-not $modelInstalled) {
    Write-Host "$Model is missing. Pulling it now..." -ForegroundColor Yellow
    & ollama pull $Model
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Failed to pull $Model." -ForegroundColor Red
        exit 1
    }
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$backendDir = Join-Path $projectRoot "backend"
$frontendDir = Join-Path $projectRoot "frontend"
$frontendEnv = Join-Path $frontendDir ".env.local"
$frontendEnvExample = Join-Path $frontendDir ".env.local.example"

if (-not (Test-Path $frontendEnv)) {
    Copy-Item $frontendEnvExample $frontendEnv
}

if (-not (Test-Path (Join-Path $frontendDir "node_modules"))) {
    Write-Host "Frontend dependencies are missing. Running npm install..." -ForegroundColor Yellow
    Push-Location $frontendDir
    try {
        & npm install
        if ($LASTEXITCODE -ne 0) { throw "npm install failed." }
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
