# AI Avatar — Local Qwen Visual Novel Agent (Updated 2026 Oct)

## Overview

AI Avatar is a local-first embodied AI project built around a visual-novel-style interface.

The project uses a local Qwen model for dialogue and semantic avatar decisions, CosyVoice3 for local text-to-speech, and SQLite as the authoritative store for persistent conversations and runtime telemetry.

The LLM is responsible for semantic decisions such as what to say, what emotion to express, and what gesture to request. Deterministic C# code remains responsible for validation, application state, persistence, animation policy, timing, and TTS control.

The current version uses keyboard text input and local speech output for CosyVoice3-supported languages. Unsupported speech languages remain available as text without invoking TTS. No cloud API key is required for the main runtime path.

![AI Avatar UI](assets/avatar-ui.png)

## Problem

A conversational avatar needs to do more than generate text.

The system needs to coordinate several independent concerns:

- language generation
- avatar emotion
- avatar gesture
- local speech synthesis
- runtime state
- authoritative conversation persistence
- atomic and idempotent chat turns
- inference telemetry
- async lifecycle isolation
- responsive UI behavior

Allowing the LLM to directly control low-level animation, application state, database operations, or arbitrary TTS parameters would make the system difficult to validate and debug.

The backend also cannot treat browser-provided conversation history as authoritative application state. The frontend sends only the current user intent, while persisted conversation history is owned by SQLite and reconstructed by ASP.NET Core.

Long-running model and speech requests introduce another class of state problem. Resetting or starting a newer turn must invalidate stale HTTP, TTS, replay, audio, and timer completions so an older async operation cannot overwrite the current browser session.

The goal of this project is

> Making a small local language model drive an embodied character while keeping low-level behavior deterministic, observable, and under application control.

## Tech Stack

- Next.js
- React
- TypeScript
- Tailwind CSS 4
- shadcn/ui
- Base UI
- ASP.NET Core
- .NET 10
- C# 14
- Entity Framework Core
- SQLite
- Ollama
- Qwen 4B
- CosyVoice3
- Python 3.10
- PyTorch

## Trade off

### Local models instead of cloud APIs

The project currently uses local Qwen inference through Ollama and local CosyVoice3 speech synthesis.

This increases local hardware requirements and makes inference slower than some hosted APIs. It also requires managing model files and Python dependencies locally.

However, it keeps the primary runtime self-contained and allows the project to experiment with model behavior, telemetry, prompting, and future fine-tuning without depending on a cloud provider.

The current runtime is therefore centered around:

- Ollama -> local LLM inference
- Qwen -> dialogue and semantic avatar decisions
- ASP.NET Core -> validation and application control
- CosyVoice3 -> local speech synthesis
- SQLite -> authoritative conversation and telemetry persistence
- Next.js -> avatar interface and interaction

### Semantic LLM control instead of direct avatar control

The LLM does not directly control CSS transforms, animation distances, runtime state, database operations, or raw speech parameters.

Instead, Qwen returns a structured semantic decision:

```json
{
  "speech": "你好。今天想聊些什么？",
  "language": "zh",
  "emotion": "happy",
  "emotionIntensity": 0.65,
  "gesture": "nod",
  "gestureIntensity": 0.35
}
```

C# validates the result, normalizes the language code, and converts the decision into deterministic application behavior.

For example:

```text
Qwen
 ↓
sad + jump
 ↓
C# motion policy
 ↓
sad + none
```

This adds application logic, but makes avatar behavior easier to validate, test, and extend.

## Why I used SQLite

Earlier versions kept the active conversation only in frontend state.

That works for a simple single-session prototype, but becomes difficult once the project needs to store multiple conversations, messages, LLM telemetry, and TTS telemetry.

JSON was also considered, but the data now has clear relationships:

```text
Conversation
    │
    └── Message
          ├── LlmTelemetry
          └── TtsTelemetry
```

The project currently stores:

- Conversations
- Messages
- LlmTelemetry
- TtsTelemetry

SQLite is also the authoritative source of conversation history. The browser does not submit a complete message history to the model path. It sends the target `conversationId`, one stable `turnId`, and the current `message`; ASP.NET Core loads the persisted history from SQLite and constructs the model context on the server.

A completed chat turn is persisted as one database transaction. Conversation creation when required, the user message, the assistant message, its structured avatar fields, and LLM telemetry are committed together. A persistence failure rolls the turn back instead of leaving a partially completed conversation.

`turnId` provides idempotency across retries. If the server already completed a turn but the client did not receive the response, retrying the same logical turn reuses the same identifier and returns the persisted result instead of creating duplicate messages.

SQLite fits the current project because the application is still local-first and normally runs for one user on one machine.

It provides:

- relational data modeling
- transactions
- indexes
- foreign keys
- SQL queries
- EF Core migrations
- a single local database file
- no separate database server

The database is stored locally at:

```text
backend/data/avatar.db
```

PostgreSQL would provide stronger server-side concurrency and multi-user capabilities, but that complexity is not currently required.

If the project later becomes a remote multi-user service, PostgreSQL would be a more appropriate next step.

## Why I used Entity Framework Core

The ASP.NET Core backend uses Entity Framework Core as the persistence layer instead of writing SQL directly throughout the application.

Database access is kept behind `ConversationStore` so avatar, Ollama, and speech logic do not depend directly on SQLite. `ChatTurnService` owns the application-level chat-turn boundary and coordinates idempotency, server-owned history, Ollama inference, and the final atomic persistence commit.

Conceptually:

```text
API Endpoint
 ↓
ChatTurnService
 ├──────────────→ Ollama
 ↓
ConversationStore
 ↓
Entity Framework Core
 ↓
SQLite
```

This keeps persistence concerns separate from Qwen, CosyVoice, and frontend behavior while making one chat turn an explicit application operation.

EF Core migrations are also committed to Git, while generated database files are ignored.

## Why I used Qwen with Ollama

The current model is:

```text
qwen3:4b-instruct-2507-q4_K_M
```

A small local model is useful for experimenting with constrained structured output, personality, semantic emotion, gesture selection, and future model specialization.

The current architecture also makes it possible to compare smaller and larger models without changing the rest of the avatar system.

Ollama provides the local model runtime and HTTP API while ASP.NET Core owns application-level validation, conversation state, persistence, and telemetry.

The server-side `OllamaRequestFactory` owns request construction and the model-context limit. For an existing conversation, ASP.NET Core loads the recent persisted messages from SQLite, appends the current user message, and sends that server-owned context to Ollama. The frontend therefore does not duplicate context-window policy or provide authoritative conversation history.

## Why I used CosyVoice3

The project uses:

```text
FunAudioLLM/Fun-CosyVoice3-0.5B-2512
```

CosyVoice3 provides local text-to-speech and supports reference-based voice generation.

A custom avatar voice can be defined through:

```text
tools/cosyvoice/voice/reference.wav
tools/cosyvoice/voice/reference.txt
```

Qwen returns the language of each assistant response as a normalized ISO 639-1 code together with the semantic avatar decision.

The current speech path allows:

```text
zh / en / ja / ko / de / es / fr / it / ru
```

If the response language is not supported by the current CosyVoice3 speech path, such as Swedish (`sv`), the frontend does not send a synthesis request.

Instead:

```text
Qwen speech + language
     ↓
C# validation
     ↓
frontend speech policy
     ├─ supported language
     │       ↓
     │   synthesizing
     │       ↓
     │   CosyVoice3
     │       ↓
     │      WAV
     │
     └─ unsupported language
             ↓
        unsupported
             ↓
        text only
```

The `unsupported` speech state is informational rather than an error. The avatar speech status and the telemetry sidebar both show `Unsupported language`, and both return to the normal state when the next user message begins.

Qwen does not directly choose unrestricted TTS parameters. For supported languages, emotion still passes through the bounded C# TTS policy before synthesis.

The current project keeps the full CosyVoice runtime dependency path rather than maintaining a custom dependency-pruned environment, because some upstream packages are imported indirectly during model initialization.

## Conversation Persistence

A new browser chat turn contains only the current intent and identity:

```text
conversationId
turnId
message
```

A new conversation begins with `conversationId = null`. The frontend generates a stable `turnId` for the logical send operation.

```text
First user message
       ↓
conversationId = null
turnId = new UUID
message = current text
       ↓
ASP.NET Core
       ↓
Check whether turnId already completed
       ↓
Load persisted conversation history from SQLite
       ↓
Append current user message
       ↓
Qwen
       ↓
Validated Avatar Decision
speech + language + emotion + gesture
       ↓
BEGIN SQLite transaction
       ↓
Create Conversation when required
       ↓
Store User Message + turnId
       ↓
Store Assistant Message + turnId + avatar fields
       ↓
Store LLM Telemetry
       ↓
COMMIT
       ↓
Return conversationId + assistantMessageId
```

Ollama inference happens before the write transaction. A model failure therefore does not leave a newly created conversation or a user-only partial turn in SQLite, while the database transaction is not held open during a potentially slow model call.

Later messages reuse the same `conversationId`. The backend, rather than the browser, reconstructs the recent conversation window from persisted messages.

Retries reuse the same logical `turnId`. If that turn already exists, the backend validates that the request matches the persisted operation and returns the completed result instead of invoking Qwen and inserting the messages again. The database also has a unique turn/role constraint as the final concurrency safeguard.

The persisted assistant message includes `language`, emotion, gesture, and intensity fields so an idempotent retry can reconstruct the same `AvatarDecision` without calling Qwen again.

Each successful assistant response also receives an `assistantMessageId`.

That identifier connects the generated text with its TTS telemetry:

```text
Conversation
     ↓
Assistant Message
     ├─ LlmTelemetry
     └─ TtsTelemetry
```

Resetting the frontend conversation starts a new browser session locally, but does not delete previously persisted SQLite records. Reset also invalidates the previous async session so stale chat, speech, replay, audio, or timer completions cannot restore the old conversation into the new UI state.

## Telemetry

The project records model execution information instead of relying only on values displayed in the UI.

LLM telemetry currently includes:

- model
- total inference duration
- model load duration
- prompt tokens
- output tokens

TTS telemetry currently includes:

- model
- voice source
- synthesis duration
- CUDA usage
- FP16 usage
- audio duration field
- real-time factor field

This allows later versions of the project to compare different models and optimization strategies using measured data instead of subjective impressions.

For example:

```text
Qwen 4B vs Qwen 8B

FP32 vs FP16

different reference voices

different TTS policies

different prompt configurations
```

## Shared UI system

The UI architecture separates responsibilities across three layers:

- shadcn/ui and Base UI provide UI primitives
- Tailwind CSS handles component styling and layout
- native CSS handles avatar composition, animation, and shared visual behavior

Native CSS is kept for cases where it is clearer than utility classes, such as:

- avatar animations
- pseudo-elements
- complex gradients
- `color-mix()`
- character composition
- runtime motion variables

The project intentionally keeps Tailwind classes compact and avoids a large responsive-token layer.

Shared UI logic follows the DRY (Don't Repeat Yourself) principle by keeping genuinely shared knowledge[^1] and behavior[^2] in a single source of truth.

Generic UI primitives do not know about Qwen, avatar emotions, gestures, CosyVoice, or SQLite.

## Responsive Design

The current interface uses a deliberately simple responsive strategy focused on predictable Full HD desktop behavior.

Components use straightforward Tailwind breakpoint utilities instead of a large system of shared responsive sizing tokens.

For example:

```text
Viewport
    ↓
Tailwind breakpoint utilities
    ↓
page grid / composer layout / typography adjustments
```

At Full HD desktop widths, the main page keeps a large avatar area beside a fixed-width telemetry sidebar.

On narrower screens, the layout stacks naturally instead of trying to proportionally scale the whole interface.

The telemetry sidebar can scroll vertically when available height is limited.

The project avoids page-level:

```text
zoom
transform: scale(...)
clamp()-driven whole-page sizing
complex responsive token systems
```

Instead, only the layout pieces that need adaptation use simple breakpoint rules:

- main page columns
- avatar stage minimum height
- input and send-button layout
- dialogue typography

The current priority is a clear and stable Full HD desktop layout, with a reasonable stacked fallback for smaller screens rather than exhaustive tuning for every viewport size.

[^1]: Knowledge: shared rules, definitions, configuration, and facts that the system needs to know.
[^2]: Behavior: reusable logic or processing that the system performs.

## Architecture

```text
User keyboard input
        ↓
Next.js
message + conversationId + turnId
        ↓
ASP.NET Core
        ↓
ChatTurnService
        ├──────────────────────────────→ SQLite
        │                                ├─ idempotency lookup
        │                                └─ persisted conversation history
        ↓
Ollama
        ↓
Qwen 4B
        ↓
Structured Avatar Decision
speech + language + emotion + gesture
        ↓
C# Validation
        ↓
Atomic persistence transaction
        ├──────────────────────────────→ SQLite
        │                                ├─ Conversations
        │                                ├─ Messages
        │                                └─ LlmTelemetry
        ↓
Next.js session state
        ├─ reducer
        ├─ session / turn identity
        └─ stale async result rejection
        ↓
TTS Policy
        ├─ supported language
        │       ↓
        │   CosyVoice3
        │       ↓
        │      WAV
        │       ↓
        │ Browser Audio
        │       ↓
        │ SQLite + TTS telemetry
        │
        └─ unsupported language
                ↓
           text only
        ↓
Avatar UI + expression + gesture
```

## Runtime Workflow

```text
idle
 ↓
User message
 ↓
stop previous speech / clear expression timer
 ↓
create or reuse turnId
 ↓
thinking
 ↓
POST conversationId + turnId + message
 ↓
backend idempotency check
 ↓
SQLite persisted history
 ↓
Qwen
 ↓
Assistant decision + language
 ↓
atomic SQLite chat-turn commit
 ↓
frontend session / turn identity check
 ↓
speech language check
 ├─ supported
 │      ↓
 │ synthesizing
 │      ↓
 │ CosyVoice3
 │      ↓
 │ SQLite + TTS telemetry
 │      ↓
 │ speaking
 │      ↓
 │ audio playback ends
 │
 └─ unsupported
        ↓
   Unsupported language
        ↓
      text only
 ↓
identity-aware expression hold
 ↓
neutral / idle
```

Text generation remains single-flight. While speech is synthesizing or playing, the composer remains available; sending the next user message stops the current speech, invalidates the previous turn generation, and immediately starts the next text turn.

Reset is a lifecycle boundary rather than only a visual state reset. It increments the browser session identity, aborts the active chat request, clears retry transport state, stops and invalidates speech, clears the neutral-expression timer, and resets reducer-owned session state. Async work that completes after that boundary is ignored when its session or turn identity is stale.

For an unsupported speech language, no CosyVoice3 synthesis request is made. The avatar speech status and the right-side Voice status display `Unsupported language` until the next user message resets the shared speech state.

If TTS fails, the already successful text response remains available.

Speech synthesis is treated as an enhancement layer rather than a dependency for successful text interaction.

## Project Structure

```text
frontend/
├─ app/
│  ├─ globals.css
│  ├─ layout.tsx
│  └─ page.tsx
├─ components/
│  ├─ ui/
│  ├─ AvatarStage.tsx
│  ├─ ChatComposer.tsx
│  ├─ ChatLogModal.tsx
│  ├─ ChatPanel.tsx
│  └─ TelemetrySidebar.tsx
├─ hooks/
│  ├─ chatSessionState.ts
│  ├─ useAvatarSpeech.ts
│  ├─ useChatSession.ts
│  ├─ useChatSpeechLifecycle.ts
│  ├─ useChatTurnTransport.ts
│  └─ useNeutralDecisionTimer.ts
├─ lib/
│  ├─ api.ts
│  └─ utils.ts
└─ types/
   └─ chat.ts

backend/
├─ Data/
│  ├─ AvatarDbContext.cs
│  └─ Entities/
├─ Endpoints/
│  ├─ ChatEndpoints.cs
│  ├─ HealthEndpoints.cs
│  └─ SpeechEndpoints.cs
├─ Migrations/
├─ Models/
├─ Options/
├─ Services/
│  ├─ Ollama/
│  │  ├─ AvatarDecisionSchema.cs
│  │  ├─ OllamaClient.cs
│  │  ├─ OllamaRequestFactory.cs
│  │  └─ OllamaResponseParser.cs
│  ├─ Persistence/
│  │  ├─ ChatTurnConflictException.cs
│  │  ├─ ConversationStore.cs
│  │  └─ IConversationStore.cs
│  ├─ Speech/
│  ├─ AvatarDecisionValidator.cs
│  ├─ AvatarMotionPolicy.cs
│  ├─ AvatarSystemPrompt.cs
│  ├─ ChatRequestValidator.cs
│  └─ ChatTurnService.cs
├─ data/
│  └─ avatar.db
├─ Program.cs
└─ appsettings.json

scripts/
├─ run-dev.ps1
├─ setup.ps1
├─ setup-cosyvoice.ps1
└─ verify.ps1

RUN_WINDOWS.cmd
SETUP_WINDOWS.cmd
VERIFY_WINDOWS.cmd
RUN_COSYVOICE_WINDOWS.cmd
SETUP_COSYVOICE_WINDOWS.cmd

tools/
├─ cosyvoice/
│  ├─ .venv/
│  ├─ CosyVoice/
│  ├─ models/
│  └─ voice/
└─ cosyvoice-service/
   └─ server.py
```

## Development

Requirements:

```text
Windows 11
.NET 10 SDK
Node.js 24+
npm
Python 3.10
Git
Ollama
```

Install CosyVoice:

```powershell
.\SETUP_COSYVOICE_WINDOWS.cmd
```

Install the project:

```powershell
.\SETUP_WINDOWS.cmd
```

The setup script checks the required local tools, starts Ollama when necessary, pulls the configured Qwen model if it is missing, restores the .NET backend, installs frontend dependencies, and runs the frontend checks.

The configured Qwen model can also be pulled manually:

```powershell
ollama pull qwen3:4b-instruct-2507-q4_K_M
```

Run:

```powershell
.\RUN_WINDOWS.cmd
```

`RUN_WINDOWS.cmd` is the Windows entry point and delegates the development startup logic to `scripts/run-dev.ps1`.

The startup script attempts to start the Ollama Windows application without blocking indefinitely, then starts the ASP.NET Core backend and Next.js frontend.

The backend starts the configured CosyVoice service automatically when local TTS is enabled. If a process is already listening on the configured CosyVoice port, the backend reuses that process instead of starting another instance.

A stale CosyVoice Python process can therefore survive an earlier development run. One observed symptom was `/health` returning successfully while `/synthesize` immediately returned HTTP 500 with `[Errno 22] Invalid argument`. Restarting Windows cleared the stale local process and restored synthesis. If the same symptom appears again, fully stop the existing CosyVoice/Python process or restart Windows before changing application code.

If Ollama is not ready within the short startup window, backend and frontend startup still continues so the project does not remain stuck at the Ollama launch step.

Local services:

```text
Frontend:   http://localhost:3000
Backend:    http://localhost:5191
Ollama:     http://localhost:11434
CosyVoice:  http://127.0.0.1:8188
```

## Database

The SQLite database is:

```text
backend/data/avatar.db
```

It can be inspected with tools such as:

- DBeaver
- DataGrip
- SQLiteStudio
- DB Browser for SQLite
- VS Code SQLite extensions

`Messages` stores the persisted chat content together with `TurnId`, `Language`, emotion, gesture, and intensity metadata. User and assistant messages from one logical chat turn share a `TurnId`; a filtered unique `(TurnId, Role)` index prevents duplicate user or assistant rows for the same idempotent operation.

Example:

```sql
SELECT *
FROM Messages
ORDER BY CreatedAtUtc DESC;
```

Inspect one logical turn:

```sql
SELECT *
FROM Messages
WHERE TurnId = 'your-turn-id'
ORDER BY CreatedAtUtc;
```

LLM telemetry:

```sql
SELECT *
FROM LlmTelemetry
ORDER BY CreatedAtUtc DESC;
```

TTS telemetry:

```sql
SELECT *
FROM TtsTelemetry
ORDER BY CreatedAtUtc DESC;
```

## Design Principle

> **LLM for semantic decisions. Deterministic code for control.**

The project treats the browser as a source of user intent rather than authoritative conversation state. SQLite owns persisted conversation history, ASP.NET Core owns chat-turn orchestration and validation, and the frontend accepts async results only while their session and turn identity is still current.

The project explores how a small local language model can drive an embodied interface while keeping runtime state, persistence, motion, validation, retry behavior, async lifecycle, and speech-generation behavior under deterministic application control.

