@echo off
REM This file launches Ollama, the .NET 10 backend, the Next.js frontend, and the browser; the backend auto-starts CosyVoice3 when configured.
setlocal

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\run-dev.ps1"
if errorlevel 1 (
  echo.
  echo Startup failed. Read the error above.
  pause
  exit /b 1
)

endlocal
