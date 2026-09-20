# AI Avatar MVP — Windows 11 / Qwen 4B / Ollama

This file explains the complete local text-interaction MVP, its current stable toolchain, architecture, and fastest way to run it.

This version is Windows 11 first. It does **not** require a cloud AI API. The browser sends text to an ASP.NET Core backend, the backend calls a local Ollama model, validates a small avatar-control protocol, and the Next.js UI renders the reply with expression sprites and lightweight motion.

## Stable stack snapshot — 2026-09-20

The project is intentionally pinned to current stable releases rather than old tutorial versions:

| Layer | Version / target |
| --- | --- |
| .NET | .NET 10 LTS / `net10.0` |
| C# | C# 14 |
| Recommended .NET SDK | 10.0.401 or newer stable .NET 10 patch |
| Next.js | 16.3.5 |
| React / React DOM | 19.3.0 |
| Tailwind CSS | 4.3.3 |
| TypeScript | 7.0.2 |
| ESLint | 10.11.0 |
| Node.js | 24.21.0 LTS recommended |
| npm metadata target | 12.0.2 |
| Ollama | 0.34.2 stable or newer |
| Local model | `qwen3:4b-instruct-2507-q4_K_M` |

Node 26 is the newer Current branch, but this project recommends the current Node 24 LTS branch for a stable application toolchain.

## One-time setup

1. Install the current stable **.NET 10 SDK**.
2. Install **Node.js 24 LTS**.
3. Install or update **Ollama for Windows**.
4. Double-click `SETUP_WINDOWS.cmd`.

The setup script checks the toolchain, starts Ollama when necessary, downloads the Qwen 4B Q4 model, restores the .NET backend, installs the pinned frontend packages, and runs basic verification.

## Start the MVP

Double-click:

```text
RUN_WINDOWS.cmd
```

Then open:

```text
http://localhost:3000
```

Services:

```text
Frontend  http://localhost:3000
Backend   http://localhost:5191
Ollama    http://localhost:11434
```

## Architecture

```text
User text
   ↓
Next.js 16 + React 19 + Tailwind CSS 4
   ↓ HTTP
ASP.NET Core 10
   ↓
Ollama
   ↓
Qwen3-4B-Instruct-2507 Q4_K_M
   ↓ JSON Schema
AvatarDecisionValidator
   ↓
AvatarMotionPolicy
   ↓
Avatar Control Protocol
   ↓
Next.js visual actuator
   ├─ expression sprite
   └─ nod / shake / jump
```

`thinking` is an operational state controlled locally. The LLM does not decide whether the system is currently waiting for inference.

## Avatar Control Protocol

The model is constrained to this shape:

```json
{
  "speech": "short natural reply",
  "emotion": "happy",
  "emotionIntensity": 0.62,
  "gesture": "nod",
  "gestureIntensity": 0.28
}
```

Supported emotions:

```text
neutral / happy / sad / angry / surprised / confused
```

Supported gestures:

```text
none / nod / shake / jump
```

The backend validates the values again before the browser can use them.

## Project layout

```text
ai-avatar-qwen4b-modern-windows11/
├─ backend/
│  ├─ Models/
│  ├─ Options/
│  ├─ Services/
│  ├─ AiAvatar.Backend.csproj
│  ├─ Program.cs
│  └─ appsettings.json
├─ frontend/
│  ├─ app/
│  ├─ components/
│  ├─ lib/
│  ├─ public/character/
│  ├─ types/
│  ├─ eslint.config.mjs
│  ├─ next.config.ts
│  ├─ package.json
│  ├─ postcss.config.mjs
│  └─ tsconfig.json
├─ scripts/
│  ├─ run-dev.ps1
│  ├─ setup.ps1
│  └─ verify.ps1
├─ RUN_WINDOWS.cmd
├─ SETUP_WINDOWS.cmd
└─ VERIFY_WINDOWS.cmd
```

## Change the character personality

Edit `backend/appsettings.json`:

```json
"Character": {
  "DisplayName": "Avatar",
  "Personality": "..."
}
```

The frontend does not need to know the prompt.

## Change the local model later

Edit:

```json
"Ollama": {
  "Model": "qwen3:4b-instruct-2507-q4_K_M"
}
```

Your future LoRA / GGUF model can replace that value while the frontend and avatar protocol remain unchanged.

## Verify before development

Double-click:

```text
VERIFY_WINDOWS.cmd
```

It runs the backend Release build plus frontend typecheck, ESLint, and production build.

## Binary assets

PNG files cannot contain source-code comments. Their purpose is documented in `frontend/public/character/README.md`. Every text/code/config file in this project begins with a short purpose description.
