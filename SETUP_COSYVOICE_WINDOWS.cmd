@echo off
REM This file launches the Windows PowerShell installer for the local CosyVoice3 avatar TTS stack.
setlocal
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File ".\scripts\setup-cosyvoice.ps1" %*
endlocal
