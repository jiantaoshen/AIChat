REM This file is a CMD wrapper that runs the full backend and frontend verification pipeline on Windows.
@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0verify.ps1"
if errorlevel 1 (
  echo.
  echo Verification failed. Read the error above.
  pause
  exit /b 1
)
echo.
pause
endlocal
