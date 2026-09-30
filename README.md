# AI Avatar — Local Qwen Visual Novel Agent

A local-first AI avatar project built with **Next.js**, **React**, **TypeScript**, **shadcn/ui**, **Base UI**, **Tailwind CSS 4**, **ASP.NET Core**, **Ollama**, **Qwen 4B**, and **CosyVoice3**.

The current MVP uses **text input** and local **text-to-speech**. Qwen handles semantic decisions such as dialogue, emotion, and gesture, while deterministic C# code owns validation, application state, timing, motion policy, and bounded TTS behavior.

No cloud API key is required for the main runtime path.

![AI Avatar UI](assets/avatar-ui.png)

## Features

- Local Qwen inference through Ollama
- Local CosyVoice3 text-to-speech
- ASP.NET Core / .NET 10 backend
- Next.js + React + TypeScript frontend
- shadcn/ui + Base UI primitives
- Tailwind CSS 4 styling and layout
- Visual-novel-style half-body avatar interface
- Custom character sprites for multiple expressions
- Emotion states: `neutral`, `happy`, `sad`, `angry`, `surprised`, `confused`
- Operational states: `idle`, `thinking`, `synthesizing`, `speaking`, `error`
- Gesture states: `none`, `nod`, `shake`, `jump`
- Structured JSON response protocol
- C# validation and deterministic motion policy
- C# TTS policy that converts semantic emotion into bounded voice instructions
- Automatic return to `neutral` after speech finishes
- Replay of the latest successfully generated avatar voice
- Scrollable conversation log dialog
- Custom reference voice support through `reference.wav` + `reference.txt`
- Python 3.10 project-local `venv`; Conda is not required
- Responsive UI driven by shared CSS design tokens and media-query overrides
- No global `zoom`, `scale`, or whole-page transform-based responsiveness

## Architecture

```text
User text
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
C# Validation
   ├─ Motion Policy
   └─ TTS Policy
        ↓
     CosyVoice3
        ↓
      WAV audio
        ↓
Avatar UI + expression + gesture + voice playback
```

The model returns semantic intent instead of low-level rendering commands.

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

The LLM handles **semantic decisions**.

Deterministic application code handles **validation, runtime state, timing, animation rules, and TTS control**.

## Runtime Flow

A normal assistant turn follows this sequence:

```text
idle
 ↓
thinking
 ↓
Qwen response
 ↓
synthesizing
 ↓
CosyVoice3 WAV generation
 ↓
speaking
 ↓
voice playback ends
 ↓
short expression hold
 ↓
neutral / idle
```

If TTS fails, the textual AI response remains available. Voice generation is an enhancement layer, not a dependency for basic chat rendering.

## Avatar Protocol

The model does **not** directly control CSS transforms, animation distances, application state, or audio parameters.

It only returns semantic values:

```text
speech
emotion
emotionIntensity
gesture
gestureIntensity
```

The backend validates the result and applies deterministic rules before the frontend renders it.

Example:

```text
sad + jump
    ↓
C# motion policy
    ↓
sad + none
```

This keeps low-level avatar behavior under application control instead of giving the LLM direct control over rendering.

## Local Text-to-Speech

The current voice path uses **CosyVoice3** with:

```text
FunAudioLLM/Fun-CosyVoice3-0.5B-2512
```

The backend keeps the local CosyVoice service available at:

```text
http://127.0.0.1:8188
```

The service is started with the project-local Python environment:

```text
tools/cosyvoice/.venv/Scripts/python.exe
```

### Custom avatar voice

To use a custom avatar voice, add:

```text
tools/cosyvoice/voice/reference.wav
tools/cosyvoice/voice/reference.txt
```

`reference.txt` must match the words spoken in `reference.wav`.

A clean reference clip is recommended:

- one speaker
- little or no background music
- little room echo
- clear pronunciation
- several seconds of natural speech

If these files are missing, the current setup can fall back to the bundled CosyVoice demo prompt voice.

### Emotion and voice behavior

Qwen returns semantic emotion only:

```text
happy
sad
angry
surprised
confused
neutral
```

C# maps those values into bounded TTS instructions. The LLM does not directly choose arbitrary pitch, speed, or synthesis parameters.

Conceptually:

```text
Qwen emotion
   ↓
C# TTS policy
   ↓
controlled CosyVoice instruction
   ↓
CosyVoice3 synthesis
```

## UI

The main interface follows a visual-novel-style composition:

- character stage in the main content area
- current AI reply displayed over the stage
- user input below the stage
- telemetry / control panel on the right on desktop
- conversation history inside a modal log window
- replay button for the latest synthesized voice

The layout is intentionally fluid at the viewport level while individual components remain bounded and readable.

```text
Viewport
│
├─ App shell ─────── fluid
├─ Main stage ────── fluid / minmax(0, 1fr)
├─ Sidebar ───────── token-driven / capped
├─ Avatar ────────── composition-driven
├─ Dialogue ──────── capped
├─ Input area ────── token-driven
└─ Typography ────── bounded
```

## Responsive Design

The frontend uses a **media-query-driven responsive system powered by shared CSS design tokens**.

Components consume responsive size and layout variables instead of hard-coding their own viewport breakpoints. Media queries override the shared tokens at carefully selected viewport ranges.

```text
Viewport / device size
        ↓
CSS media queries
        ↓
responsive design-token overrides
        ↓
components consume shared tokens
        ↓
layout adapts independently by component
```

The page does **not** rely on global `zoom`, `transform: scale(...)`, or other whole-interface scaling techniques.

Instead, different UI areas adapt independently:

- page gutter
- shell columns
- sidebar width
- avatar composition
- dialogue width and padding
- input height and button width
- telemetry columns
- modal dimensions
- typography sizing

This allows the interface to remain balanced across phones, tablets, laptops, Full HD, QHD, ultrawide displays, and larger screens without making every component grow at the same rate.

Large breakpoints selectively enhance the large-screen layout instead of scaling the complete interface.

The responsive token system currently includes targeted adjustments for:

```text
base / mobile
40rem   → large phone / small tablet
48rem   → tablet
72rem   → laptop / desktop two-column layout
90rem   → large laptop / wider desktop
120rem  → Full HD class layout enhancement
140rem+ → wider character composition changes
160rem+ → QHD and larger
240rem  → very large / 4K-class composition cap
```

Short-height landscape and laptop screens also receive independent height-based adjustments.

## Shared UI System

The frontend divides UI responsibilities into three layers:

```text
shadcn/ui + Base UI
        ↓
UI primitives

Tailwind CSS
        ↓
component styling + local layout

CSS variables + native CSS
        ↓
design tokens + responsive tokens + effects that are clearer as CSS
```

### shadcn/ui and Base UI

Reusable UI primitives live under:

```text
frontend/components/ui/
```

Current primitives include:

```text
badge.tsx
button.tsx
card.tsx
dialog.tsx
scroll-area.tsx
textarea.tsx
```

These primitives own reusable component behavior and shared variants.

They should not know about Qwen, avatar emotions, gestures, or CosyVoice.

### Tailwind CSS

Tailwind is used directly in TSX for:

- layout
- flex / grid composition
- spacing
- typography
- local sizing
- borders
- component-level visual adjustments

For example, a sidebar button can remain a standard shared `Button`:

```tsx
<Button
  type="button"
  size="lg"
  className="w-full"
  onClick={onReplay}
  disabled={!canReplay}
>
  <Volume2Icon />
  Replay voice
</Button>
```

There is no separate page-specific button CSS class when the shared primitive plus local Tailwind utilities already express the design.

### CSS variables and native CSS

`frontend/app/globals.css` owns shared design-system and responsive values such as:

```text
colors
semantic theme tokens
radius
page gutter
layout gap
sidebar width
avatar composition
input dimensions
dialog dimensions
responsive overrides
```

Native CSS is also used where it is clearer than utility classes, including:

- keyframe animations
- pseudo-elements
- complex gradients
- `color-mix()`
- character visual composition
- runtime CSS-variable-driven avatar motion

## DRY Principle

The UI follows **DRY — Don't Repeat Yourself**, but the goal is to avoid duplicated **knowledge and rules**, not merely duplicated syntax.

> **Don't repeat knowledge or rules, not merely syntax.**

A shared abstraction is useful when several parts of the application genuinely depend on the same rule or concept.

Small coincidental repetitions are preferred over unnecessary general-purpose abstractions.

Examples:

```text
Shared button appearance
→ Button primitive

Shared responsive sidebar width
→ CSS design token

Avatar emotion meaning
→ avatar domain logic

Replay audio behavior
→ speech hook / domain logic
```

This keeps generic UI infrastructure independent from avatar-specific behavior.

## Tech Stack

### Frontend

- Next.js 16
- React 19
- TypeScript
- Tailwind CSS 4
- shadcn/ui
- Base UI
- class-variance-authority
- Lucide icons

### Backend

- ASP.NET Core
- .NET 10
- C# 14

### Local AI

- Ollama
- `qwen3:4b-instruct-2507-q4_K_M`

### Local TTS

- Python 3.10
- project-local `venv`
- PyTorch
- torchaudio
- CosyVoice3
- `FunAudioLLM/Fun-CosyVoice3-0.5B-2512`
- FastAPI / Uvicorn service

## Models

The main runtime uses two model stacks:

```text
LLM
└─ qwen3:4b-instruct-2507-q4_K_M

TTS
└─ FunAudioLLM/Fun-CosyVoice3-0.5B-2512
```

There is currently **no speech-input / Push-to-Talk / local ASR feature** in this build.

The `openai-whisper` Python package may appear inside the CosyVoice Python dependency environment, but this project does not use a Whisper ASR model for microphone input.

## Project Structure

```text
frontend/
├─ app/
│  ├─ globals.css
│  ├─ layout.tsx
│  └─ page.tsx
├─ components/
│  ├─ ui/
│  │  ├─ badge.tsx
│  │  ├─ button.tsx
│  │  ├─ card.tsx
│  │  ├─ dialog.tsx
│  │  ├─ scroll-area.tsx
│  │  └─ textarea.tsx
│  ├─ AvatarStage.tsx
│  ├─ ChatPanel.tsx
│  ├─ ChatLogModal.tsx
│  └─ TelemetrySidebar.tsx
├─ hooks/
│  └─ useAvatarSpeech.ts
├─ lib/
│  ├─ api.ts
│  └─ utils.ts
├─ public/
│  ├─ backgrounds/
│  │  └─ beijing-palace.png
│  └─ character/
│     ├─ neutral.png
│     ├─ happy.png
│     ├─ sad.png
│     ├─ angry.png
│     ├─ surprised.png
│     ├─ confused.png
│     └─ thinking.png
└─ types/
   └─ chat.ts

backend/
├─ Models/
├─ Options/
├─ Services/
│  └─ Speech/
│     ├─ CosyVoiceClient.cs
│     ├─ CosyVoiceServerHostedService.cs
│     └─ TtsSpeechPolicy.cs
├─ Program.cs
└─ appsettings.json

tools/
├─ cosyvoice/
│  ├─ .venv/
│  ├─ CosyVoice/
│  ├─ models/
│  └─ voice/
└─ cosyvoice-service/
   └─ server.py

scripts/
├─ setup.ps1
├─ setup-cosyvoice.ps1
├─ run-dev.ps1
└─ verify.ps1
```

## Run Locally

### 1. Requirements

Recommended Windows development environment:

- Windows 11 x64
- .NET 10 SDK
- Node.js 24+
- npm
- Python 3.10 x64
- Git
- Ollama
- NVIDIA GPU recommended for faster CosyVoice inference

No Conda installation is required.

### 2. Pull the Qwen model

```powershell
ollama pull qwen3:4b-instruct-2507-q4_K_M
```

Optional model test:

```powershell
ollama run qwen3:4b-instruct-2507-q4_K_M
```

### 3. Install CosyVoice3

From the project root:

```powershell
.\SETUP_COSYVOICE_WINDOWS.cmd
```

The installer creates a project-local Python environment under:

```text
tools/cosyvoice/.venv
```

The default setup can select CUDA PyTorch when an NVIDIA environment is available. You can also request a backend explicitly:

```powershell
.\SETUP_COSYVOICE_WINDOWS.cmd -TorchBackend cpu
```

or:

```powershell
.\SETUP_COSYVOICE_WINDOWS.cmd -TorchBackend cu121
```

### 4. Install frontend and backend dependencies

```powershell
.\SETUP_WINDOWS.cmd
```

### 5. Configure the frontend API URL

The frontend uses:

```env
NEXT_PUBLIC_API_BASE_URL=http://localhost:5191
```

The example file is:

```text
frontend/.env.local.example
```

### 6. Start the project

```powershell
.\RUN_WINDOWS.cmd
```

The launcher starts the local development services and opens the frontend.

Local addresses:

```text
Frontend:   http://localhost:3000
Backend:    http://localhost:5191
Ollama:     http://localhost:11434
CosyVoice:  http://127.0.0.1:8188
```

### 7. Run CosyVoice separately for debugging

If you want to inspect TTS logs directly:

```powershell
.\RUN_COSYVOICE_WINDOWS.cmd
```

Health check:

```powershell
Invoke-RestMethod http://127.0.0.1:8188/health
```

A healthy service should report that the model is ready.

## Backend Configuration

Important settings live in:

```text
backend/appsettings.json
```

The main sections are:

```text
Ollama
CosyVoice
Character
Logging
```

Example responsibilities:

```text
Ollama
→ endpoint, model, context, temperature, token limit

CosyVoice
→ service path, model path, reference voice, timeout, port

Character
→ display name and personality
```

## Conversation and Replay Voice

The latest successfully synthesized WAV is retained by the frontend as a browser object URL.

```text
CosyVoice3 response
      ↓
WAV Blob
      ↓
URL.createObjectURL(...)
      ↓
hasReplay = true
      ↓
Replay voice enabled
```

The Replay button does **not** synthesize the text again. It replays the most recent generated audio.

Resetting the conversation clears that cached replay audio.

## Current Scope

Included:

- keyboard text input
- local Qwen chat inference
- structured avatar emotion and gesture output
- deterministic motion policy
- local CosyVoice3 TTS
- custom reference voice support
- replay voice
- conversation log
- responsive visual-novel UI

Not currently included:

- Push-to-Talk
- microphone input
- Whisper ASR
- VAD
- realtime streaming ASR
- Live2D
- realtime lip sync

These can be added later without changing the central architecture because input perception, language reasoning, avatar policy, and speech output are kept as separate layers.

## Design Principle

> **LLM for semantic decisions. Deterministic code for control.**

The project explores how a small local language model can drive an embodied interface without directly controlling low-level rendering, runtime state, motion parameters, or unrestricted voice-generation behavior.
