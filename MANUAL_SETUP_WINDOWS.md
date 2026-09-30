<!-- This file provides manual Windows 11 setup steps for the text-input + local Qwen + CosyVoice3 Python-venv avatar build. -->
# Manual setup — Windows 11

## 1. Install prerequisites

Install:

- .NET 10 SDK x64
- Node.js 24 or newer
- Python 3.10 x64
- Ollama
- Git

Verify Python with:

```powershell
py -3.10 --version
```

## 2. Pull Qwen

```powershell
ollama pull qwen3:4b-instruct-2507-q4_K_M
```

## 3. Install CosyVoice3 into the project venv

```powershell
.\SETUP_COSYVOICE_WINDOWS.cmd
```

The Python interpreter used by the backend will be:

```text
tools/cosyvoice/.venv/Scripts/python.exe
```

For your own avatar voice, create:

```text
tools/cosyvoice/voice/reference.wav
tools/cosyvoice/voice/reference.txt
```

For a CPU-only TTS install:

```powershell
.\SETUP_COSYVOICE_WINDOWS.cmd -TorchBackend cpu
```

## 4. Backend

```powershell
cd backend
dotnet restore
dotnet build -c Release
dotnet run
```

Backend: `http://localhost:5191`

The backend automatically starts the local CosyVoice Python service when configured to do so.

## 5. Frontend

Open a second PowerShell window:

```powershell
cd frontend
npm install
Copy-Item .env.local.example .env.local
npm run dev
```

Frontend: `http://localhost:3000`

## 6. Optional manual TTS service

For troubleshooting:

```powershell
.\RUN_COSYVOICE_WINDOWS.cmd
```

Then open `http://127.0.0.1:8188/health`.

## 7. Runtime flow

```text
Text input
  ↓
Qwen
  ↓
validated speech / emotion / gesture
  ↓
CosyVoice3 WAV synthesis
  ↓
browser playback + avatar animation
```

There is no speech-input / ASR layer in this build.
