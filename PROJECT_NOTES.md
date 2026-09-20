# Project notes

This file records the engineering decisions behind the MVP so later LoRA, RAG, voice, or renderer work can evolve without rewriting the core architecture.

## MVP boundary

Current scope:

```text
text input → local Qwen → structured semantic decision → sprite/motion feedback
```

Not included yet:

```text
microphone / ASR / TTS / RAG / long-term memory / LoRA training / Live2D
```

## Responsibility split

Qwen is responsible for:

```text
natural reply
emotion
emotion intensity
optional abstract gesture
```

C# is responsible for:

```text
validation
state control
motion policy
Ollama connectivity
error handling
input limits
```

Next.js is responsible for:

```text
user input
conversation presentation
thinking feedback
sprite selection
CSS motion rendering
telemetry display
```

## Why Qwen3-4B-Instruct-2507

The project deliberately uses the non-thinking instruct model. A realtime avatar benefits more from predictable latency and direct short replies than from long visible reasoning. The selected Ollama Q4_K_M build is small enough for local experimentation while preserving the same model family for later LoRA work.

## Future narrow specialization

The intended future direction is not to make the model universally stronger. It is to specialize it for:

```text
document-grounded conversation
multi-turn chat
stable character preference
short natural replies
emotion/action selection
Chinese / English / Swedish interaction
```

Documents and changing knowledge should primarily enter through retrieval/context, while LoRA/SFT/preference tuning teaches the model how to use that context and how the character should respond.
