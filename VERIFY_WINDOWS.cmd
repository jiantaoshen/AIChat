@echo off
REM This file is the root-level Windows verification entry point for backend compilation plus frontend typecheck, lint, and production build.
setlocal

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\verify.ps1"
if errorlevel 1 (
  echo.
  echo Verification failed. Read the error above.
  pause
  exit /b 1
)

echo.
pause
endlocal
