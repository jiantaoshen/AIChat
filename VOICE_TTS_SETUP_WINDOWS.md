<!-- This file contains only CosyVoice/TTS-specific runtime behavior, voice configuration, manual health checks, and troubleshooting. General Windows installation belongs in WINDOWS_SETUP.md. -->
# Local Avatar TTS — CosyVoice3

General Windows prerequisites and installation are intentionally not repeated here. Follow [WINDOWS_SETUP.md](WINDOWS_SETUP.md) for the canonical setup sequence.

This project runs **FunAudioLLM/Fun-CosyVoice3-0.5B-2512** behind a local FastAPI service. ASP.NET Core communicates with it on `127.0.0.1:8188`, and the browser receives the generated WAV audio.

## Runtime behavior

```text
Qwen response
    ↓
speech + language + emotion + intensity
    ↓
C# validation + TtsSpeechPolicy
    ↓
supported language?
    ├─ yes → CosyVoice3 local service → WAV → Browser Audio
    └─ no  → text only
```

The LLM does not control unrestricted audio parameters. C# maps semantic output into bounded TTS behavior.

The current speech path supports:

```text
zh / en / ja / ko / de / es / fr / it / ru
```

Unsupported response languages remain valid text responses and do not invoke CosyVoice. The frontend exposes an informational `unsupported` speech state instead of treating that case as a chat failure.

Text interaction does not depend on successful TTS. If synthesis fails, the assistant text remains available.

## Custom avatar voice

Custom reference files live at:

```text
tools/cosyvoice/voice/reference.wav
tools/cosyvoice/voice/reference.txt
```

Recommended reference audio:

- 3–10 seconds
- one speaker only
- no music
- little or no reverb
- clear speech
- a real WAV file rather than another codec renamed to `.wav`

The text file should match the spoken recording exactly. Example:

```text
你好，很高兴见到你。今天想聊些什么？
```

Only use a voice sample you have permission to use.

If the custom reference files are missing and the configured fallback is enabled, the service uses the bundled CosyVoice demo prompt voice so the synthesis path can still be tested.

## Manual service and health check

Normally the ASP.NET Core backend starts the Python service automatically. For TTS-only troubleshooting, start it manually from the repository root:

```powershell
.\RUN_COSYVOICE_WINDOWS.cmd
```

Health endpoint:

```text
http://127.0.0.1:8188/health
```

PowerShell:

```powershell
Invoke-RestMethod http://127.0.0.1:8188/health | ConvertTo-Json
```

Useful health fields include the ready state, model, sample rate, voice source, and CUDA availability.

## Stale port 8188 process

The backend can reuse an existing listener on `127.0.0.1:8188`. If an old Python process survives a development restart, the application can therefore talk to stale TTS code or stale CUDA/runtime state.

Inspect the listener:

```powershell
$conn = Get-NetTCPConnection -LocalPort 8188 -State Listen
$conn

$pid8188 = $conn.OwningProcess
Get-CimInstance Win32_Process -Filter "ProcessId=$pid8188" |
    Select-Object ProcessId, Name, CommandLine
```

Terminate a confirmed stale CosyVoice process with:

```powershell
Stop-Process -Id $pid8188 -Force
```

Then restart the application so the current backend can start the current TTS service.

If Python/CUDA state remains inconsistent after process termination, restart Windows before changing application code. A previously observed `HTTP 500` with Python `[Errno 22] Invalid argument` was resolved by a full machine restart, which confirmed the failure was stale local runtime state rather than the chat persistence changes.

## Reference WAV check

If synthesis fails immediately, verify that the project venv can decode the reference audio:

```powershell
.\tools\cosyvoice\.venv\Scripts\python.exe -c "import torchaudio; x,sr=torchaudio.load(r'.\tools\cosyvoice\voice\reference.wav', backend='soundfile'); print(x.shape, sr)"
```

If this command fails, investigate the reference file or local audio dependencies before debugging React, ASP.NET Core, SQLite, or Ollama.

## Native Windows venv limitation

The upstream CosyVoice stack can use `wetext`/Pynini for text normalization. Pynini does not publish normal native Windows wheels on PyPI, so this project's native Windows venv omits that dependency and uses the available tokenizer path instead.

Ordinary avatar dialogue works without the full text-normalization frontend, but unusual numbers, abbreviations, dates, or symbols may be normalized less completely than on a full supported upstream environment.

If a remaining dependency requires native compilation, install Visual Studio Build Tools with **Desktop development with C++** and rerun the canonical setup described in [WINDOWS_SETUP.md](WINDOWS_SETUP.md).

## `openai-whisper` / `pkg_resources` installation error

The Windows TTS setup pins `setuptools<81` and installs `openai-whisper==20231117` without build isolation because that legacy package still depends on `pkg_resources` during its build.

If an older venv was created before that fix and remains broken, rerun the canonical TTS installation step from [WINDOWS_SETUP.md](WINDOWS_SETUP.md). The existing venv does not normally need to be deleted first.
