<!-- This is the canonical Windows installation document. Keep shared prerequisites, versions, setup commands, startup, and verification here instead of copying them into other docs. -->
# Windows 11 setup

This file is the **single canonical Windows setup document** for the project.

`README.md` should only link here for installation. `VOICE_TTS_SETUP_WINDOWS.md` contains TTS-specific operation and troubleshooting after installation; it does not duplicate the general setup sequence.

## Required software

Install:

- Windows 11 x64
- .NET 10 SDK x64
- Node.js 24 or newer, including npm
- Python 3.10 x64 from python.org with the Windows Python Launcher (`py.exe`)
- Ollama
- Git

Verify from PowerShell:

```powershell
dotnet --version
node --version
npm --version
py -3.10 --version
ollama --version
git --version
```

The pinned Qwen model used by the application is:

```text
qwen3:4b-instruct-2507-q4_K_M
```

CosyVoice model and voice-specific details are owned by [VOICE_TTS_SETUP_WINDOWS.md](VOICE_TTS_SETUP_WINDOWS.md).

## First-time setup

Run commands from the repository root.

### 1. Install the local CosyVoice3 runtime

```powershell
.\SETUP_COSYVOICE_WINDOWS.cmd
```

This creates the project-local TTS runtime under:

```text
tools/cosyvoice/
├─ .venv/
├─ CosyVoice/
├─ models/Fun-CosyVoice3-0.5B-2512/
└─ voice/
```

The default setup selects CUDA 12.1 PyTorch when `nvidia-smi` is available and CPU wheels otherwise.

Optional setup modes:

```powershell
.\SETUP_COSYVOICE_WINDOWS.cmd -TorchBackend cpu
.\SETUP_COSYVOICE_WINDOWS.cmd -TorchBackend cu121
.\SETUP_COSYVOICE_WINDOWS.cmd -ModelSource modelscope
```

No Conda installation is required by this project.

For custom reference voice requirements and TTS-specific behavior, see [VOICE_TTS_SETUP_WINDOWS.md](VOICE_TTS_SETUP_WINDOWS.md).

### 2. Install the application dependencies

```powershell
.\SETUP_WINDOWS.cmd
```

The setup script:

- validates the required .NET, Node/npm, and Ollama tools
- starts Ollama when necessary
- pulls the pinned Qwen model when it is missing
- restores and builds the .NET backend
- creates `frontend/.env.local` from the example when required
- installs frontend dependencies from `frontend/package-lock.json` with `npm ci`
- runs frontend type checking and linting

The committed lockfile is authoritative for frontend dependency installation. `npm install` is only a fallback in development when the lockfile is absent; reproducible verification requires the lockfile.

## Start

```powershell
.\RUN_WINDOWS.cmd
```

Local services:

```text
Frontend:   http://localhost:3000
Backend:    http://localhost:5191
Ollama:     http://localhost:11434
CosyVoice:  http://127.0.0.1:8188
```

The backend automatically starts the local CosyVoice service when TTS is enabled and no compatible service is already listening on the configured port.

This build intentionally contains no microphone, Push-to-Talk, Whisper, or ASR input path.

## Verify

Run the repository verification pipeline with:

```powershell
.\VERIFY_WINDOWS.cmd
```

Verification performs:

```text
.NET restore
.NET Release build
npm ci from package-lock.json
TypeScript typecheck
ESLint
Next.js production build
```

`package-lock.json` is required by the verification script so CI-style checks are reproducible instead of depending on an existing `node_modules` directory.

## Manual recovery commands

Use these only when diagnosing the normal root scripts.

Backend:

```powershell
cd backend
dotnet restore
dotnet build -c Release
dotnet run
```

Frontend in a second PowerShell window:

```powershell
cd frontend
npm ci
Copy-Item .env.local.example .env.local -ErrorAction SilentlyContinue
npm run dev
```

If `frontend/.env.local` already exists, keep the existing file instead of overwriting local configuration.

For manual CosyVoice service startup, health checks, reference voice configuration, or TTS-specific failures, use [VOICE_TTS_SETUP_WINDOWS.md](VOICE_TTS_SETUP_WINDOWS.md).

## Database

The local SQLite database is:

```text
backend/data/avatar.db
```

EF Core migrations are committed to the repository. Apply pending migrations from `backend/` with:

```powershell
dotnet ef database update
```

The database file itself is local runtime state and should not be committed.

## Troubleshooting

### Ollama is not ready

Open the Ollama Windows application, confirm `http://localhost:11434` is available, and rerun the setup or start script.

If the pinned Qwen model is missing, rerun `SETUP_WINDOWS.cmd`; it checks the local Ollama model list and pulls the model named above when required.

### Frontend dependency state is inconsistent

Do not repair a lockfile-based install with a casual `npm install` first. From `frontend/`, prefer:

```powershell
npm ci
```

This removes the existing install and recreates it from `package-lock.json`.

### TTS service returns HTTP 500 or an old process is still on port 8188

See the TTS-specific troubleshooting section in [VOICE_TTS_SETUP_WINDOWS.md](VOICE_TTS_SETUP_WINDOWS.md). A stale Python/CUDA process can survive application restarts; terminate the old listener or restart Windows before changing application code.
