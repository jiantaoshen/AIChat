REM This file is a CMD wrapper that launches the Windows development environment through PowerShell with a temporary execution-policy bypass.
@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-dev.ps1"
if errorlevel 1 (
  echo.
  echo Startup failed. Read the error above.
  pause
  exit /b 1
)
endlocal
