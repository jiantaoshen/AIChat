# AI Avatar — Local Qwen Visual Novel Agent (Updated 2026 Oct)

## Overview

AI Avatar is a local-first embodied AI project built around a visual-novel-style interface.

A local Qwen model generates dialogue and semantic avatar decisions, ASP.NET Core owns application control and persistence, SQLite is the authoritative conversation store, and CosyVoice3 provides local text-to-speech for supported languages.

The project is intentionally server-authoritative: the browser sends the current user intent, not an authoritative copy of conversation history. The backend reconstructs model context from persisted SQLite messages, executes each chat turn with a stable `turnId`, and commits completed turns atomically.

The browser also treats Reset and newer turns as lifecycle boundaries. Stale chat, TTS, replay, audio, and timer completions are invalidated instead of being allowed to overwrite the current session.

![AI Avatar UI](assets/avatar-ui.png)

## Architecture

```text
User keyboard input
        ↓
Next.js
        │
        │  conversationId + turnId + message
        ↓
ASP.NET Core
        ↓
ChatTurnService
        ├─ idempotency lookup
        ├─ load persisted conversation history
        ├─ build bounded Ollama context
        └─ call Ollama / Qwen
                    ↓
          structured avatar decision
          speech + language + emotion + gesture
                    ↓
              C# validation
                    ↓
        atomic SQLite turn commit
        ├─ Conversation when required
        ├─ User Message
        ├─ Assistant Message
        └─ LLM Telemetry
                    ↓
              Next.js UI
        ├─ reducer-owned session state
        ├─ stale async invalidation
        └─ supported language?
              ├─ yes → CosyVoice3 → WAV → Browser Audio
              └─ no  → text only
```

Core design rules:

- **Server-authoritative conversation state** — SQLite owns persisted history; the client sends intent, not history.
- **Atomic chat turns** — a completed turn is committed as one persistence unit instead of a series of partial saves.
- **Idempotent retries** — the same logical retry reuses its `turnId` so a lost response does not duplicate messages.
- **Deterministic control** — Qwen chooses semantic output; C# owns validation, persistence, motion, timing, and TTS policy.
- **Lifecycle isolation** — Reset and newer turns invalidate stale HTTP, speech, replay, audio, and timer completions.
- **Local-first runtime** — Qwen, SQLite, and CosyVoice3 run locally without a cloud API dependency on the main path.

## Runtime

A normal text turn follows this path:

```text
idle
 ↓
user message
 ↓
backend idempotency check
 ↓
SQLite history + current message
 ↓
Qwen
 ↓
validated assistant decision
 ↓
atomic SQLite commit
 ↓
text response
 ↓
language check
 ├─ supported   → synthesizing → speaking → idle
 └─ unsupported → text only    → idle
```

Text generation is single-flight. Speech synthesis and playback are interruptible: sending the next user message stops current speech and starts the next text turn.

Reset starts a new browser session lifecycle. Work started by an older session is cancelled when possible and ignored if it completes after that boundary.

## Quick start

First-time Windows installation is documented in one place:

- [Windows 11 setup](WINDOWS_SETUP.md)

After setup:

```powershell
.\RUN_WINDOWS.cmd
```

Open:

```text
http://localhost:3000
```

Run the reproducible verification pipeline with:

```powershell
.\VERIFY_WINDOWS.cmd
```

## Documentation

Documentation ownership is intentionally narrow to avoid duplicated setup instructions:

- [WINDOWS_SETUP.md](WINDOWS_SETUP.md) — **canonical Windows installation, prerequisites, startup, verification, and recovery commands**.
- [VOICE_TTS_SETUP_WINDOWS.md](VOICE_TTS_SETUP_WINDOWS.md) — **TTS-specific behavior only**: reference voice, supported languages, manual TTS health checks, and CosyVoice troubleshooting.
- [PROJECT_NOTES.md](PROJECT_NOTES.md) — current engineering scope, responsibility boundaries, invariants, and code ownership.

Do not copy installation requirements or setup command sequences into additional documents. Update the canonical owner and link to it instead.

## Main project structure

```text
frontend/
├─ components/                 # avatar, composer, telemetry, log UI
├─ hooks/
│  ├─ chatSessionState.ts      # reducer/state transitions
│  ├─ useChatSession.ts        # session orchestration
│  ├─ useChatTurnTransport.ts  # chat request, retry identity, abort
│  ├─ useChatSpeechLifecycle.ts
│  ├─ useAvatarSpeech.ts
│  └─ useNeutralDecisionTimer.ts
├─ lib/api.ts
└─ types/chat.ts

backend/
├─ Data/                       # EF Core DbContext + entities
├─ Endpoints/                  # HTTP routes
├─ Migrations/                 # committed EF Core migrations
├─ Services/
│  ├─ ChatTurnService.cs       # one logical chat-turn boundary
│  ├─ Ollama/                  # Qwen request/response integration
│  ├─ Persistence/             # authoritative conversation storage
│  └─ Speech/                  # CosyVoice integration + TTS policy
└─ data/avatar.db              # local SQLite database

tools/
├─ cosyvoice/                  # project-local venv, upstream repo, model, voice
└─ cosyvoice-service/          # local FastAPI wrapper
```

## Design principle

> **LLM for semantic decisions. Deterministic code for control.**

The project explores how a small local language model can drive an embodied interface while keeping conversation truth, persistence, retries, runtime state, motion, validation, and speech behavior under deterministic application control.
