@echo off
REM This file manually starts the local CosyVoice3 FastAPI service with the project-local Python 3.10 venv for troubleshooting or when backend AutoStart is disabled.
setlocal
cd /d "%~dp0"
set "PYTHON=%~dp0tools\cosyvoice\.venv\Scripts\python.exe"
if not exist "%PYTHON%" (
  echo [ERROR] CosyVoice Python venv was not found.
  echo Run SETUP_COSYVOICE_WINDOWS.cmd first.
  exit /b 1
)
"%PYTHON%" ".\tools\cosyvoice-service\server.py" --cosyvoice-repo ".\tools\cosyvoice\CosyVoice" --model-dir ".\tools\cosyvoice\models\Fun-CosyVoice3-0.5B-2512" --reference-wav ".\tools\cosyvoice\voice\reference.wav" --reference-text ".\tools\cosyvoice\voice\reference.txt" --host 127.0.0.1 --port 8188 --use-official-demo-voice
endlocal
