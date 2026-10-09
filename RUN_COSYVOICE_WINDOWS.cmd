@echo off
REM Thin Windows wrapper. Canonical CosyVoice configuration lives in backend\appsettings.json.
setlocal
cd /d "%~dp0"
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\run-cosyvoice.ps1"
set "EXIT_CODE=%ERRORLEVEL%"
endlocal & exit /b %EXIT_CODE%
