# This file runs the reproducible verification pipeline: backend tests/build plus frontend tests, typecheck, ESLint, and production build.
$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$backendDir = Join-Path $projectRoot "backend"
$backendTestsProject = Join-Path $projectRoot "tests\AiAvatar.Backend.Tests\AiAvatar.Backend.Tests.csproj"
$frontendDir = Join-Path $projectRoot "frontend"
$frontendLock = Join-Path $frontendDir "package-lock.json"

Write-Host "Restoring and building ASP.NET Core 10 backend..." -ForegroundColor Cyan
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

Write-Host "Running backend tests against deterministic policy and real SQLite behavior..." -ForegroundColor Cyan
& dotnet restore $backendTestsProject
if ($LASTEXITCODE -ne 0) { throw "Backend test restore failed." }

& dotnet test $backendTestsProject -c Release --no-restore --minimum-expected-tests 1
if ($LASTEXITCODE -ne 0) { throw "Backend tests failed." }

Write-Host "Checking Next.js 16 frontend..." -ForegroundColor Cyan
Push-Location $frontendDir
try {
    if (-not (Test-Path $frontendLock)) {
        throw "package-lock.json is required for reproducible frontend verification."
    }

    Write-Host "Installing frontend dependencies exactly from package-lock.json with npm ci..." -ForegroundColor Cyan
    & npm ci
    if ($LASTEXITCODE -ne 0) { throw "npm ci failed." }

    & npm run test
    if ($LASTEXITCODE -ne 0) { throw "Frontend tests failed." }

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
