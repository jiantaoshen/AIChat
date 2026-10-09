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
backend speechCapability
    ├─ supported   → frontend may request /api/speech
    │                  ↓
    │              backend re-checks persisted language
    │                  ↓
    │              CosyVoice3 → WAV → Browser Audio
    └─ unsupported → text only
```

The LLM does not control unrestricted audio parameters. C# maps semantic output into bounded TTS behavior.

The canonical supported-language set is owned by backend `TtsSpeechPolicy`. It is intentionally not duplicated in TypeScript or this document. The Chat API returns `speechCapability.supported`, which the frontend consumes for UI behavior.

Unsupported response languages remain valid text responses and do not invoke CosyVoice. The frontend exposes an informational `unsupported` speech state instead of treating that case as a chat failure. Direct calls to `/api/speech` are also protected: the backend reloads the persisted assistant message, checks its persisted language with `TtsSpeechPolicy`, and rejects unsupported languages before contacting CosyVoice.

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

`RUN_COSYVOICE_WINDOWS.cmd` is intentionally only a thin wrapper. The actual launcher is `scripts/run-cosyvoice.ps1`, which reads the same canonical `CosyVoice` section from `backend/appsettings.json` that ASP.NET Core binds at runtime. Model path, reference voice, host, port, and demo-voice fallback are therefore not duplicated in the CMD file. Standard `CosyVoice__...` environment variables remain explicit overrides, matching ASP.NET Core configuration precedence.

Health endpoint:

```text
http://127.0.0.1:8188/health
```

PowerShell:

```powershell
Invoke-RestMethod http://127.0.0.1:8188/health | ConvertTo-Json
```

Useful health fields include:

```text
service
contractVersion
ready
model
modelPath
voiceSource
referenceWavPath
referenceTextPath
sampleRate
cudaAvailable
```

The backend does not trust a TCP listener by itself. Before reusing an already-running process it calls `/health` and verifies the expected service identity/contract, `ready=true`, the configured model, and the resolved voice WAV/text references.

## Stale port 8188 process

An old Python process can survive a development restart. The backend now fails closed instead of treating "port 8188 is open" as proof that the correct CosyVoice service is running.

Reuse is allowed only when the `/health` response matches the current canonical configuration. If the port is occupied by an unidentified HTTP/TCP service, an older health contract, a different model, or a different reference voice, backend startup reports the mismatch and tells you to stop the stale listener. It does not silently reuse it.

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
