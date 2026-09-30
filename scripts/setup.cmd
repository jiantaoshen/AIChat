REM This file is a CMD wrapper that runs the one-time Windows PowerShell setup without requiring a permanent execution-policy change.
@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup.ps1"
if errorlevel 1 (
  echo.
  echo Setup failed. Read the error above.
  pause
  exit /b 1
)
echo.
pause
endlocal
