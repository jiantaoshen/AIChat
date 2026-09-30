<!-- This file explains the native Windows Python-venv setup used to run local CosyVoice3 text-to-speech without Conda. -->
# Local Avatar TTS — CosyVoice3 with Python venv

The project runs **Fun-CosyVoice3-0.5B-2512** behind a small local FastAPI service. ASP.NET Core talks to it on `127.0.0.1`, and the browser receives the final WAV audio.

## 1. Prerequisites

Install:

- Windows 11 x64
- Python **3.10 x64** from python.org
- Git
- enough disk space for the repository, venv, and model weights

When installing Python, keep the **Python Launcher (`py.exe`)** enabled. Verify:

```powershell
py -3.10 --version
```

No Conda installation is required by this project.

## 2. Install CosyVoice3

From the repository root:

```powershell
.\SETUP_COSYVOICE_WINDOWS.cmd
```

The script creates:

```text
tools/
└─ cosyvoice/
   ├─ .venv/                             # Python 3.10 virtual environment
   ├─ CosyVoice/                         # official repository
   ├─ models/Fun-CosyVoice3-0.5B-2512/ # model weights
   └─ voice/                             # avatar voice reference
```

Use ModelScope instead of Hugging Face if desired:

```powershell
.\SETUP_COSYVOICE_WINDOWS.cmd -ModelSource modelscope
```

Force CPU or CUDA 12.1 PyTorch:

```powershell
.\SETUP_COSYVOICE_WINDOWS.cmd -TorchBackend cpu
.\SETUP_COSYVOICE_WINDOWS.cmd -TorchBackend cu121
```

With the default `auto`, the script chooses CUDA 12.1 wheels when `nvidia-smi` exists and CPU wheels otherwise.

## 3. Windows venv note

The upstream CosyVoice installation documentation recommends Conda. One reason is `wetext`, which depends on Pynini. Pynini does not publish native Windows wheels on PyPI.

This project's venv installer therefore uses the official CosyVoice requirements **except `wetext`/Pynini**. Current CosyVoice can run without a text-normalization frontend and falls back to its tokenizer path. This is appropriate for ordinary avatar dialogue, but text normalization for unusual numbers, abbreviations, dates, or symbols may be less polished than the full Linux/Conda stack.

If pip reports that another package must be compiled, install **Visual Studio Build Tools** with **Desktop development with C++**, then rerun setup.

## 4. Custom avatar voice

Add:

```text
tools/cosyvoice/voice/reference.wav
tools/cosyvoice/voice/reference.txt
```

Recommended reference audio:

- 3–10 seconds
- one speaker only
- no music
- little/no reverb
- clear speech

The text file must match the recording exactly. Example:

```text
你好，很高兴见到你。今天想聊些什么？
```

Only use a voice sample you have permission to use.

If these files are missing, the project falls back to CosyVoice's bundled demo prompt voice so the TTS pipeline can still be tested.

## 5. Start / test CosyVoice manually

```powershell
.\RUN_COSYVOICE_WINDOWS.cmd
```

Health endpoint:

```text
http://127.0.0.1:8188/health
```

Normally manual startup is unnecessary because the ASP.NET Core backend launches the venv Python process automatically when `CosyVoice:AutoStart` is enabled.

## 6. Runtime flow

```text
Qwen response
    ↓
speech + emotion + intensity
    ↓
C# TtsSpeechPolicy
    ↓
CosyVoice3 local service
    ↓
WAV
    ↓
Browser Audio
    ↓
speaking state
```

The LLM never gets direct control of raw audio parameters. C# maps semantic emotion into bounded TTS instructions.

### `openai-whisper` / `pkg_resources` install error

If an earlier venv setup failed with `ModuleNotFoundError: No module named 'pkg_resources'`, rerun `SETUP_COSYVOICE_WINDOWS.cmd`. The setup script now pins `setuptools<81` and installs `openai-whisper==20231117` with build isolation disabled, which avoids the incompatible temporary build environment. You do not need to delete the existing venv first.

