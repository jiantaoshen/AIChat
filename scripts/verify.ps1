# This file runs the reproducible verification pipeline: .NET Release build plus Next.js typecheck, ESLint, and production build.
$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$backendDir = Join-Path $projectRoot "backend"
$frontendDir = Join-Path $projectRoot "frontend"

Write-Host "Building ASP.NET Core 10 backend..." -ForegroundColor Cyan
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

Write-Host "Checking Next.js 16 frontend..." -ForegroundColor Cyan
Push-Location $frontendDir
try {
    if (-not (Test-Path "node_modules")) {
        & npm install
        if ($LASTEXITCODE -ne 0) { throw "npm install failed." }
    }

    & npm run typecheck
    if ($LASTEXITCODE -ne 0) { throw "TypeScript typecheck failed." }

    & npm run lint
    if ($LASTEXITCODE -ne 0) { throw "ESLint failed." }

    & npm run build
    if ($LASTEXITCODE -ne 0) { throw "Next.js production build failed." }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "All verification steps passed." -ForegroundColor Green
