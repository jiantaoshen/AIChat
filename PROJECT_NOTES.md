<!-- This file summarizes current engineering scope, invariants, and code ownership. Installation instructions belong in WINDOWS_SETUP.md. -->
# Project notes

The current build is a **text-input local AI Avatar MVP**.

## Responsibility split

```text
Qwen
→ dialogue semantics
→ response language
→ emotion
→ gesture intent

ASP.NET Core / C#
→ validation
→ server-authoritative conversation state
→ chat-turn orchestration
→ idempotency and atomic persistence
→ motion policy
→ TTS policy
→ service orchestration

Next.js / React
→ avatar rendering
→ dialogue UI
→ browser session lifecycle
→ WAV playback
→ conversation log
```

## Core invariants

- SQLite is the authoritative source of persisted conversation history.
- The frontend sends `conversationId`, stable `turnId`, and the current `message`; it does not supply authoritative history.
- The backend owns the bounded Ollama context window.
- A completed chat turn is persisted atomically.
- Retrying the same logical operation reuses its `turnId` and must not duplicate the turn.
- Reset creates a new browser session lifecycle; stale work from an older session cannot update the new one.
- TTS is an enhancement layer. Text remains successful when speech is unsupported or synthesis fails.

## Code ownership

```text
frontend/components/ChatPanel.tsx
→ page composition only

frontend/components/ChatComposer.tsx
→ text input + send UI only

frontend/hooks/chatSessionState.ts
→ reducer state + session transitions

frontend/hooks/useChatSession.ts
→ high-level browser session orchestration

frontend/hooks/useChatTurnTransport.ts
→ chat request, AbortController, stable retry turnId

frontend/hooks/useChatSpeechLifecycle.ts
→ speech/replay lifecycle coordination

frontend/hooks/useAvatarSpeech.ts
→ audio synthesis/playback generation guards

frontend/hooks/useNeutralDecisionTimer.ts
→ identity-aware expression reset timing

backend/Endpoints/*
→ HTTP route behavior

backend/Services/ChatTurnService.cs
→ one logical chat-turn boundary

backend/Services/Persistence/ConversationStore.cs
→ authoritative history + atomic SQLite persistence

backend/Services/Ollama/OllamaRequestFactory.cs
→ Ollama payload + bounded model-context window

backend/Services/Ollama/OllamaResponseParser.cs
→ Ollama envelope + avatar JSON + telemetry parsing

backend/Services/Ollama/OllamaClient.cs
→ Ollama HTTP transport/status only
```

## Documentation ownership

Do not duplicate installation steps here.

- `README.md` — overview, architecture, quick start, links.
- `WINDOWS_SETUP.md` — canonical Windows prerequisites, installation, startup, verification, recovery.
- `VOICE_TTS_SETUP_WINDOWS.md` — TTS-specific operation and troubleshooting only.
- `PROJECT_NOTES.md` — engineering boundaries and invariants.

Future areas may include RAG, long-term memory, LoRA/preference tuning, and Live2D/3D rendering.
