<!-- This file summarizes the current engineering scope and responsibility boundaries of the local AI Avatar MVP. -->
# Project notes

The current build is a **text-input local AI Avatar MVP**.

Core stack:

```text
Next.js + shadcn/ui + Tailwind CSS
ASP.NET Core / .NET 10
Ollama + Qwen 4B
CosyVoice3 TTS
```

Responsibility split:

```text
Qwen
→ conversation semantics
→ emotion
→ gesture intent

C#
→ validation
→ operational state
→ motion policy
→ TTS policy
→ service orchestration

Next.js
→ avatar rendering
→ dialogue UI
→ WAV playback
→ conversation log
```

Current operational states:

```text
idle
thinking
synthesizing
speaking
error
```

Speech input, microphone capture, Push-to-Talk, and ASR are intentionally not part of this build. They can be added later as a separate perception/input layer without changing the Qwen or avatar-decision protocol.

Future areas may include RAG, long-term memory, LoRA/preference tuning, and Live2D/3D rendering.
