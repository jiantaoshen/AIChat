@echo off
REM This file runs the base Windows setup for .NET, Node, Ollama/Qwen, and frontend/backend dependencies; CosyVoice3 is installed separately with SETUP_COSYVOICE_WINDOWS.cmd.
setlocal

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\setup.ps1"
if errorlevel 1 (
  echo.
  echo Setup failed. Read the error above.
  pause
  exit /b 1
)

echo.
pause
endlocal
