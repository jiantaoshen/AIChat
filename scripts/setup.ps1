# This file performs one-time Windows 11 setup checks, starts Ollama, pulls the Qwen model configured by the backend, installs dependencies, and verifies the project.
$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot "project-config.ps1")
$ollamaConfig = Get-ProjectOllamaConfig -ProjectRoot $projectRoot
$Model = $ollamaConfig.Model
$OllamaBaseUrl = $ollamaConfig.BaseUrl
$MinimumDotNetMajor = 10
$RecommendedNodeMajor = 24
$RecommendedOllama = [version]"0.34.2"

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

function Test-OllamaServer {
    try {
        Invoke-RestMethod -Uri "$OllamaBaseUrl/api/tags" -TimeoutSec 2 | Out-Null
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

    Start-Process -FilePath $ollamaExe -ArgumentList @("serve") -WindowStyle Hidden | Out-Null
    return $true
}

function Ensure-OllamaServer {
    if (Test-OllamaServer) {
        return
    }

    Write-Host "Starting Ollama Windows app..." -ForegroundColor Cyan

    if (-not (Start-OllamaWindowsApp)) {
        throw "Could not locate the Ollama Windows application."
    }

    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        if (Test-OllamaServer) {
            return
        }
        Start-Sleep -Seconds 1
    }

    throw "Ollama did not become reachable at $OllamaBaseUrl. Open the Ollama Windows app and retry."
}

function Get-VersionFromText {
    param([string]$Text)

    $match = [regex]::Match($Text, '(\d+)\.(\d+)\.(\d+)')
    if (-not $match.Success) {
        return $null
    }

    return [version]$match.Value
}

Require-Command "dotnet" "Install the latest stable .NET 10 SDK, reopen PowerShell, and run setup again."
Require-Command "node" "Install Node.js 24 LTS or newer, reopen PowerShell, and run setup again."
Require-Command "npm" "npm is normally installed together with Node.js."
Require-Command "ollama" "Install the current stable Ollama for Windows, then reopen PowerShell."

$dotnetVersion = Get-VersionFromText (& dotnet --version)
if ($null -eq $dotnetVersion -or $dotnetVersion.Major -lt $MinimumDotNetMajor) {
    throw ".NET 10 SDK or newer is required. Installed: $dotnetVersion"
}
Write-Host ".NET SDK: $dotnetVersion" -ForegroundColor Green

$nodeVersion = Get-VersionFromText ((& node --version).TrimStart('v'))
if ($null -eq $nodeVersion -or $nodeVersion.Major -lt $RecommendedNodeMajor) {
    throw "Node.js 24 LTS or newer is required. Installed: $nodeVersion"
}
Write-Host "Node.js: $nodeVersion" -ForegroundColor Green

$npmVersion = Get-VersionFromText (& npm --version)
Write-Host "npm: $npmVersion" -ForegroundColor Green

$ollamaText = (& ollama --version 2>&1 | Out-String).Trim()
$ollamaVersion = Get-VersionFromText $ollamaText
if ($null -ne $ollamaVersion -and $ollamaVersion -lt $RecommendedOllama) {
    Write-Host "Ollama $ollamaVersion is older than the project snapshot ($RecommendedOllama). Updating Ollama is recommended." -ForegroundColor Yellow
} else {
    Write-Host "Ollama: $ollamaVersion" -ForegroundColor Green
}

Write-Host "Qwen model (backend/appsettings.json): $Model" -ForegroundColor Green
Ensure-OllamaServer

$tags = Invoke-RestMethod -Uri "$OllamaBaseUrl/api/tags"
$modelInstalled = @($tags.models | ForEach-Object { $_.name }) -contains $Model

if (-not $modelInstalled) {
    Write-Host "Pulling $Model (about 2.5 GB)..." -ForegroundColor Cyan
    & ollama pull $Model
    if ($LASTEXITCODE -ne 0) {
        throw "ollama pull $Model failed."
    }
} else {
    Write-Host "$Model is already installed." -ForegroundColor Green
}

$frontendDir = Join-Path $projectRoot "frontend"
$frontendLock = Join-Path $frontendDir "package-lock.json"
$backendDir = Join-Path $projectRoot "backend"
$frontendEnv = Join-Path $frontendDir ".env.local"
$frontendEnvExample = Join-Path $frontendDir ".env.local.example"

if (-not (Test-Path $frontendEnv)) {
    Copy-Item $frontendEnvExample $frontendEnv
}

Write-Host "Restoring .NET 10 backend..." -ForegroundColor Cyan
Push-Location $backendDir
try {
    & dotnet restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed." }

    & dotnet build -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed." }
}
finally {
    Pop-Location
}

Write-Host "Installing pinned Next.js/Tailwind dependencies..." -ForegroundColor Cyan
Push-Location $frontendDir
try {
    if (Test-Path $frontendLock) {
        & npm ci
        if ($LASTEXITCODE -ne 0) { throw "npm ci failed." }
    }
    else {
        Write-Host "package-lock.json is missing. Falling back to npm install..." -ForegroundColor Yellow
        & npm install
        if ($LASTEXITCODE -ne 0) { throw "npm install failed." }
    }

    & npm run typecheck
    if ($LASTEXITCODE -ne 0) { throw "frontend typecheck failed." }

    & npm run lint
    if ($LASTEXITCODE -ne 0) { throw "frontend lint failed." }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "Setup complete." -ForegroundColor Green
Write-Host "Run RUN_WINDOWS.cmd to start the MVP."
