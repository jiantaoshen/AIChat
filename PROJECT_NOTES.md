<!-- This file summarizes current engineering scope, invariants, and code ownership. Installation instructions belong in WINDOWS_SETUP.md. Keep ownership entries synchronized with the implementation when responsibilities move between files. -->
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
→ server-authoritative conversation state and model context budget
→ chat-turn orchestration, serialization, idempotency, and atomic persistence
→ motion policy
→ server-authoritative TTS capability and synthesis policy
→ CosyVoice service identity/configuration validation and orchestration

Next.js / React
→ avatar rendering
→ dialogue UI
→ browser session lifecycle
→ consumption of backend speech capability
→ browser WAV playback and replay
→ conversation log
```

## Core invariants

- SQLite is the authoritative source of persisted conversation history and assistant speech source data.
- The frontend sends references and intent (`conversationId`, stable `turnId`, current message, or assistant `messageId`); it does not supply authoritative conversation history or TTS text/emotion/language policy inputs.
- The backend owns model-context construction. The current user message is kept intact, and persisted history is added only as recent contiguous complete turns while the conservative estimated prompt plus reserved output remains within the configured Ollama context window.
- `ConversationContextPolicy` owns the deterministic context budget; `ConversationHistoryReader` owns complete-turn selection; `OllamaRequestFactory` serializes the already-selected model context and does not truncate it again.
- A completed chat turn is persisted atomically. Retrying the same logical operation reuses its `turnId` and must not duplicate the turn.
- Different turns for the same conversation are serialized by the backend so a later turn cannot generate from history that excludes an earlier in-flight turn. Different conversations remain independent.
- Reset creates a new browser session lifecycle; stale LLM, TTS, timer, or audio callbacks from an older session/generation cannot update the new one.
- Text generation is single-flight per browser session. A new accepted text send may interrupt TTS, but it must not preempt an active LLM turn.
- Backend `TtsSpeechPolicy` is the single authority for speech-language capability. The frontend consumes `speechCapability`; `/api/speech` independently revalidates the persisted assistant language before synthesis.
- Reusing an existing CosyVoice listener requires a compatible `/health` contract (service identity, contract version, readiness, model, and configured voice source). An open TCP port alone is never sufficient evidence of compatibility.
- `backend/appsettings.json` is the committed canonical runtime configuration source for Ollama and CosyVoice defaults. Windows launcher scripts consume that configuration rather than duplicating model/host/port/voice defaults.
- TTS is an enhancement layer. Text remains successful when speech is unsupported, synthesis fails, or the local speech dependency is unavailable.
- Deterministic policy, parser, transaction/idempotency, concurrency, context-budget, service-discovery, and async lifecycle behavior must have automated regression coverage before additional feature work expands those paths.

## Code ownership

```text
frontend/components/ChatPanel.tsx
→ page composition only

frontend/components/ChatComposer.tsx
→ text input + send UI only

frontend/hooks/chatSessionState.ts
→ reducer state + browser-session transitions

frontend/hooks/useChatSession.ts
→ high-level browser chat/session orchestration

frontend/hooks/useChatTurnTransport.ts
→ React adapter for the chat request lifecycle

frontend/hooks/chatTurnTransportController.ts
→ deterministic text single-flight + AbortController + retry turnId state machine

frontend/hooks/avatarSpeechController.ts
→ deterministic speech synthesis/playback lifecycle, generation invalidation, abort, stale-audio guards, and replay ownership

frontend/hooks/useAvatarSpeech.ts
→ React adapter around AvatarSpeechController; exposes controller state/actions to hooks/components

frontend/hooks/chatSpeechLifecyclePolicy.ts
→ turn-identity guard for speech-hold completion

frontend/hooks/useChatSpeechLifecycle.ts
→ coordinates speech/replay completion with current-turn expression/unsupported-state hold behavior

frontend/hooks/useNeutralDecisionTimer.ts
→ identity-aware expression reset timing

frontend/lib/operationalState.ts
→ operational busy-state semantics and unsupported-status hold policy

backend/Endpoints/*
→ HTTP route binding/response behavior; application policy stays in services

backend/Services/ChatTurnService.cs
→ one logical chat-turn application boundary, including idempotency flow and response speech capability

backend/Services/Concurrency/ConversationTurnGate.cs
→ process-local serialization of turns for the same conversation

backend/Services/Persistence/ChatTurnRepository.cs
→ completed-turn lookup/idempotency recovery + atomic conversation/user/assistant/LLM-telemetry commit

backend/Services/Persistence/ConversationHistoryReader.cs
→ SQLite-backed recent contiguous complete-turn selection for model context

backend/Services/Persistence/SpeechRepository.cs
→ authoritative persisted assistant speech source + TTS telemetry persistence

backend/Services/Persistence/ConversationTitlePolicy.cs
→ deterministic conversation-title formatting policy

backend/Services/Ollama/ConversationContextPolicy.cs
→ deterministic conservative context-budget calculation and message-cost estimation

backend/Services/Ollama/OllamaRequestFactory.cs
→ serialization of system prompt + already-selected model messages into the Ollama request payload only

backend/Services/Ollama/OllamaResponseParser.cs
→ Ollama envelope + avatar JSON + telemetry parsing

backend/Services/Ollama/OllamaClient.cs
→ Ollama HTTP transport/status only

backend/Services/Speech/TtsSpeechPolicy.cs
→ canonical supported-language capability + bounded TTS semantic policy

backend/Services/Speech/SpeechSynthesisService.cs
→ server-authoritative speech orchestration from persisted assistant data; revalidates capability before CosyVoice

backend/Services/Speech/CosyVoiceServiceHealthPolicy.cs
→ CosyVoice /health identity/config compatibility checks

backend/Services/Speech/CosyVoiceServerHostedService.cs
→ local CosyVoice process discovery/startup orchestration; never treats an open port as sufficient identity

backend/Services/Speech/CosyVoiceClient.cs
→ CosyVoice HTTP transport plus dependency health/identity validation before synthesis

scripts/project-config.ps1
→ reads canonical committed runtime defaults from backend/appsettings.json for launcher scripts

scripts/run-cosyvoice.ps1
→ manual CosyVoice launch implementation using canonical project configuration

RUN_COSYVOICE_WINDOWS.cmd
→ thin Windows wrapper that delegates to scripts/run-cosyvoice.ps1

tests/AiAvatar.Backend.Tests/*
→ backend policy/parser + real SQLite atomicity/idempotency/context/concurrency/TTS/service-discovery regression tests

frontend/tests/*
→ frontend single-flight/reset/retry/reducer/speech-lifecycle/stale-callback regression tests
```

## Documentation ownership

Do not duplicate installation steps or runtime policy lists here.

- `README.md` — overview, architecture, quick start, and links.
- `WINDOWS_SETUP.md` — canonical Windows prerequisites, installation, startup, verification, and recovery.
- `VOICE_TTS_SETUP_WINDOWS.md` — TTS-specific setup, operation, service-discovery behavior, and troubleshooting; implementation-owned policy lists must be referenced rather than copied.
- `PROJECT_NOTES.md` — current engineering boundaries, invariants, and code ownership. When an ownership boundary moves in code, update this file in the same change.

Future areas may include RAG, long-term memory, LoRA/preference tuning, and Live2D/3D rendering.
