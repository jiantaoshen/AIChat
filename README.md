# AI Avatar — Local Qwen Visual Novel Agent

A local-first AI avatar project built with **Next.js**, **shadcn/ui**, **Tailwind CSS**, **ASP.NET Core**, **Ollama**, and **Qwen 4B**.

The current MVP is text-only and focuses on local inference, structured emotion / gesture output, deterministic avatar control, and a visual-novel-style interface.

![AI Avatar UI](assets/avatar-ui.png)

## Features

- Local Qwen inference through Ollama
- ASP.NET Core backend
- Next.js + shadcn/ui frontend
- Visual-novel-style half-body avatar layout
- Emotion states: `neutral`, `happy`, `sad`, `angry`, `surprised`, `confused`
- Operational `thinking` state
- Gesture states: `none`, `nod`, `shake`, `jump`
- Structured JSON response protocol
- C# validation and motion policy
- Automatic return to `neutral` after temporary expressions
- Scrollable conversation log dialog
- Hybrid Fluid desktop layout for 1080p, 1440p, ultrawide, and 4K displays
- No cloud API key required

## Architecture

```text
User
  ↓
Next.js Frontend
  ↓
ASP.NET Core Backend
  ↓
Ollama
  ↓
Qwen 4B
  ↓
Structured Avatar Decision
  ↓
Validation + Motion Policy
  ↓
Avatar UI
```

Example model output:

```json
{
  "speech": "你好。今天想聊些什么？",
  "emotion": "happy",
  "emotionIntensity": 0.65,
  "gesture": "nod",
  "gestureIntensity": 0.35
}
```

The LLM handles **semantic decisions**. Deterministic C# code handles **validation, application state, timing, and motion rules**.

## UI

The main interface uses a visual-novel-style layout:

- character stage in the main area
- current AI reply displayed over the stage
- user input fixed at the bottom
- telemetry / control panel on the right
- full conversation history inside a modal log window

The layout follows a **Viewport Fluid + Component Capped** approach:

```text
Viewport
│
├─ App Shell ─────── fluid
├─ Main Stage ────── fluid / 1fr
├─ Sidebar ───────── capped
├─ Avatar ────────── capped
├─ Dialogue ──────── capped
└─ Typography ────── capped
```

This allows the UI to use large displays without scaling every component uncontrollably.

## Tech Stack

### Frontend

- Next.js
- React
- TypeScript
- Tailwind CSS 4
- shadcn/ui
- Lucide icons

### Backend

- ASP.NET Core
- .NET 10
- C#

### AI

- Ollama
- `qwen3:4b-instruct-2507-q4_K_M`

## Project Structure

```text
frontend/
├─ app/
│  ├─ globals.css
│  └─ page.tsx
├─ components/
│  ├─ ui/
│  ├─ AvatarStage.tsx
│  ├─ ChatPanel.tsx
│  ├─ ChatLogModal.tsx
│  └─ TelemetrySidebar.tsx
├─ lib/
├─ public/
│  ├─ character/
│  └─ backgrounds/
└─ types/

backend/
├─ Models/
├─ Options/
├─ Services/
├─ Program.cs
└─ appsettings.json
```

## Run Locally

### 1. Requirements

- Windows 11 x64
- .NET 10 SDK
- Node.js 24+
- Ollama

### 2. Pull the model

```powershell
ollama pull qwen3:4b-instruct-2507-q4_K_M
```

Optional test:

```powershell
ollama run qwen3:4b-instruct-2507-q4_K_M
```

### 3. Start the backend

```powershell
cd backend
dotnet restore
dotnet run
```

Default backend address:

```text
http://localhost:5191
```

### 4. Start the frontend

```powershell
cd frontend
npm install
Copy-Item .env.local.example .env.local
npm run dev
```

Make sure `.env.local` contains:

```env
NEXT_PUBLIC_API_BASE_URL=http://localhost:5191
```

Open:

```text
http://localhost:3000
```

## Avatar Protocol

The model does not directly control CSS transforms or animation values.

It only returns semantic intent:

```text
emotion + emotionIntensity
gesture + gestureIntensity
```

The backend then applies deterministic rules before rendering.

For example:

```text
sad + jump
    ↓
sad + none
```

This keeps low-level avatar behavior under application control.

## Design Principle

> **LLM for semantic decisions. Deterministic code for control.**

The project explores how a small local language model can drive an embodied interface without directly controlling low-level rendering or application state.
