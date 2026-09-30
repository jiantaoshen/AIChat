<!-- This file explains normal Windows 11 setup and startup for the text-input + Qwen + CosyVoice3 Python-venv build. -->
# Windows 11 setup

## Required software

Install:

- .NET 10 SDK x64
- Node.js 24 or newer
- Python 3.10 x64
- Ollama
- Git

Verify:

```powershell
dotnet --version
node --version
npm --version
py -3.10 --version
ollama --version
git --version
```

## Qwen

```powershell
ollama pull qwen3:4b-instruct-2507-q4_K_M
```

## CosyVoice3

```powershell
.\SETUP_COSYVOICE_WINDOWS.cmd
```

This creates a project-local Python venv at:

```text
tools/cosyvoice/.venv
```

No Conda is required. See `VOICE_TTS_SETUP_WINDOWS.md` for voice-reference and Windows-specific details.

## Project dependencies

```powershell
.\SETUP_WINDOWS.cmd
```

## Start

```powershell
.\RUN_WINDOWS.cmd
```

Open `http://localhost:3000`.

This build intentionally contains no microphone, Push-to-Talk, Whisper, or ASR code.
