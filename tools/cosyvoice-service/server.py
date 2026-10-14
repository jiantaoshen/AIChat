"""Run a local FastAPI wrapper around the official CosyVoice3 model for avatar text-to-speech synthesis."""

from __future__ import annotations

import argparse
import inspect
import io
import logging
import sys
import threading
from pathlib import Path
from typing import Any

import soundfile as sf
import torch
import uvicorn
from fastapi import FastAPI, HTTPException
from fastapi.responses import Response
from pydantic import BaseModel, Field

LOG = logging.getLogger("ai-avatar-cosyvoice")
OFFICIAL_DEMO_PROMPT_TEXT = "希望你以后能够做的比我还好呦。"
END_OF_PROMPT = "<|endofprompt|>"
ASSISTANT_PROMPT = "You are a helpful assistant."
SERVICE_ID = "ai-avatar-cosyvoice"
HEALTH_CONTRACT_VERSION = 3
REFERENCE_SPEAKER_ID = "ai-avatar-reference"
INSTRUCTION_SPEAKER_ID = "ai-avatar-reference-instruction"


class SynthesisRequest(BaseModel):
    """JSON request accepted from the ASP.NET Core backend."""

    text: str = Field(min_length=1, max_length=1200)
    instruction: str = Field(default="", max_length=1200)
    speed: float = Field(default=1.0, ge=0.75, le=1.25)


class CosyVoiceRuntime:
    """Own the loaded CosyVoice3 model and the reference voice used for zero-shot synthesis."""

    def __init__(
        self,
        cosyvoice_repo: Path,
        model_dir: Path,
        reference_wav: Path,
        reference_text: Path,
        use_official_demo_voice: bool,
        low_vram_mode: bool,
    ) -> None:
        self.cosyvoice_repo = cosyvoice_repo
        self.model_dir = model_dir
        self.reference_wav = reference_wav
        self.reference_text = reference_text
        self.use_official_demo_voice = use_official_demo_voice
        self.low_vram_mode = low_vram_mode
        self.model: Any | None = None
        self.sample_rate: int | None = None
        self.voice_source = "unconfigured"
        self.fp16_enabled = False
        self.precision_profile = "default"
        self.model_dtypes: dict[str, list[str]] = {}
        self.reference_cache_ready = False
        self.speech_tokenizer_providers: list[str] = []
        self._lock = threading.Lock()

    def load(self) -> None:
        self._configure_import_paths()
        self._resolve_reference_voice()

        # Import the upstream module before applying local overrides. The overrides mutate
        # the imported class objects used by CosyVoice3, but keep the cloned repo untouched.
        from cosyvoice.cli import cosyvoice as cosyvoice_module  # type: ignore

        if self.low_vram_mode:
            from low_vram import install_low_vram_overrides

            install_low_vram_overrides()

        LOG.info("Loading CosyVoice3 model from %s", self.model_dir)
        LOG.info("CUDA available: %s", torch.cuda.is_available())
        LOG.info("Low-VRAM mode: %s", self.low_vram_mode)

        # On the 4 GB target configuration, low-VRAM mode uses mixed precision and
        # sequential offload: LLM CUDA FP16, Flow FP32 swapped onto CUDA only for decode,
        # HiFT CPU FP32, CPU speech-tokenizer inference, no TensorRT and no vLLM.
        request_fp16 = self.low_vram_mode and torch.cuda.is_available()
        self.model = cosyvoice_module.AutoModel(
            model_dir=str(self.model_dir),
            load_trt=False,
            load_vllm=False,
            fp16=request_fp16,
        )
        self.sample_rate = int(self.model.sample_rate)
        self.model_dtypes = self._read_model_dtypes()
        self.fp16_enabled = self._verify_fp16_runtime(request_fp16)
        self.precision_profile = (
            "llm-fp16-gpu-flow-fp32-swap-hift-fp32-cpu"
            if request_fp16
            else "upstream-default"
        )
        self.speech_tokenizer_providers = self._read_speech_tokenizer_providers()
        self._cache_reference_voice()

        LOG.info(
            "CosyVoice3 ready at %s Hz using voice source '%s' (fp16=%s, tokenizer=%s).",
            self.sample_rate,
            self.voice_source,
            self.fp16_enabled,
            self.speech_tokenizer_providers or ["unknown"],
        )
        self._log_cuda_memory("after startup")

    def synthesize(self, request: SynthesisRequest) -> bytes:
        if self.model is None or self.sample_rate is None:
            raise RuntimeError("CosyVoice3 model has not been loaded.")

        text = request.text.strip()
        if not text:
            raise ValueError("Text cannot be empty.")

        instruction = self._format_instruction(request.instruction)
        chunks: list[torch.Tensor] = []

        with self._lock:
            self._reset_cuda_peak_memory()
            instruction_spk_id = ""
            try:
                # eval() alone does not disable autograd. Explicit inference_mode keeps
                # activation memory bounded and is especially important on a 4 GB card.
                with torch.inference_mode():
                    if instruction:
                        instruction_spk_id = self._prepare_instruction_reference(instruction)
                        generator = self.model.inference_instruct2(
                            text,
                            instruction,
                            str(self.reference_wav),
                            zero_shot_spk_id=instruction_spk_id,
                            stream=False,
                            speed=request.speed,
                        ) if instruction_spk_id else self.model.inference_instruct2(
                            text,
                            instruction,
                            str(self.reference_wav),
                            stream=False,
                            speed=request.speed,
                        )
                    else:
                        prompt_text = self._load_prompt_text()
                        generator = self.model.inference_zero_shot(
                            text,
                            prompt_text,
                            str(self.reference_wav),
                            zero_shot_spk_id=REFERENCE_SPEAKER_ID,
                            stream=False,
                            speed=request.speed,
                        ) if self.reference_cache_ready else self.model.inference_zero_shot(
                            text,
                            prompt_text,
                            str(self.reference_wav),
                            stream=False,
                            speed=request.speed,
                        )

                    for item in generator:
                        speech = item.get("tts_speech")
                        if speech is None:
                            continue
                        if speech.ndim == 1:
                            speech = speech.unsqueeze(0)
                        chunks.append(speech.detach().to("cpu"))
            finally:
                if instruction_spk_id:
                    self._drop_instruction_reference(instruction_spk_id)
                self._log_cuda_memory("after synthesis")
                if self.low_vram_mode and torch.cuda.is_available():
                    torch.cuda.empty_cache()

        if not chunks:
            raise RuntimeError("CosyVoice3 produced no audio samples.")

        waveform_tensor = torch.cat(chunks, dim=1).squeeze(0).float()
        self._validate_waveform(waveform_tensor)
        waveform = waveform_tensor.clamp(-1.0, 1.0).numpy()
        output = io.BytesIO()
        sf.write(output, waveform, self.sample_rate, format="WAV", subtype="PCM_16")
        output.seek(0)
        return output.read()

    def is_ready(self) -> bool:
        return (
            self.model is not None
            and self.sample_rate is not None
            and self.reference_wav.exists()
        )

    def gpu_memory_snapshot(self) -> dict[str, object] | None:
        if not torch.cuda.is_available():
            return None

        mib = 1024 * 1024
        try:
            device = torch.cuda.current_device()
            total = torch.cuda.get_device_properties(device).total_memory
            free, total_from_driver = torch.cuda.mem_get_info(device)
            return {
                "device": torch.cuda.get_device_name(device),
                "allocatedMiB": round(torch.cuda.memory_allocated(device) / mib, 1),
                "reservedMiB": round(torch.cuda.memory_reserved(device) / mib, 1),
                "peakAllocatedMiB": round(torch.cuda.max_memory_allocated(device) / mib, 1),
                "freeMiB": round(free / mib, 1),
                "totalMiB": round((total_from_driver or total) / mib, 1),
            }
        except Exception as exception:
            LOG.debug("Could not read CUDA memory telemetry: %s", exception)
            return None

    def _configure_import_paths(self) -> None:
        if not self.cosyvoice_repo.exists():
            raise FileNotFoundError(
                f"CosyVoice repository not found: {self.cosyvoice_repo}"
            )
        if not self.model_dir.exists():
            raise FileNotFoundError(f"CosyVoice model not found: {self.model_dir}")

        repo = str(self.cosyvoice_repo.resolve())
        matcha = str((self.cosyvoice_repo / "third_party" / "Matcha-TTS").resolve())

        for entry in (repo, matcha):
            if entry not in sys.path:
                sys.path.insert(0, entry)

    def _resolve_reference_voice(self) -> None:
        if self.reference_wav.exists() and self.reference_text.exists():
            text = self.reference_text.read_text(encoding="utf-8").strip()
            if text:
                self.voice_source = "custom-reference"
                return

        if not self.use_official_demo_voice:
            raise FileNotFoundError(
                "Custom reference.wav/reference.txt are missing and official demo fallback is disabled."
            )

        demo_wav = self.cosyvoice_repo / "asset" / "zero_shot_prompt.wav"
        if not demo_wav.exists():
            raise FileNotFoundError(
                "Custom voice reference is missing and the official demo reference could not be found."
            )

        self.reference_wav = demo_wav
        self.voice_source = "official-demo-fallback"
        LOG.warning(
            "Custom voice reference is missing. Using CosyVoice's bundled demo prompt voice. "
            "Add tools/cosyvoice/voice/reference.wav and reference.txt for your own avatar voice."
        )

    def _cache_reference_voice(self) -> None:
        if self.model is None:
            return

        add_zero_shot_spk = getattr(self.model, "add_zero_shot_spk", None)
        zero_shot = getattr(self.model, "inference_zero_shot", None)
        instruct2 = getattr(self.model, "inference_instruct2", None)
        if not callable(add_zero_shot_spk) or not callable(zero_shot) or not callable(instruct2):
            LOG.warning("CosyVoice reference caching is unavailable in this upstream revision.")
            return

        try:
            zero_shot_supports_id = "zero_shot_spk_id" in inspect.signature(zero_shot).parameters
            instruct_supports_id = "zero_shot_spk_id" in inspect.signature(instruct2).parameters
        except (TypeError, ValueError):
            zero_shot_supports_id = False
            instruct_supports_id = False

        if not zero_shot_supports_id or not instruct_supports_id:
            LOG.warning(
                "CosyVoice reference caching disabled because this upstream revision does not "
                "expose zero_shot_spk_id on both inference paths."
            )
            return

        try:
            prompt_text = self._load_prompt_text()
            add_zero_shot_spk(
                prompt_text,
                str(self.reference_wav),
                REFERENCE_SPEAKER_ID,
            )

            # In low-VRAM mode the frontend is already CPU-bound, but migrate the cache
            # defensively so prompt features never become persistent GPU allocations.
            if self.low_vram_mode:
                frontend = self.model.frontend
                cached = frontend.spk2info.get(REFERENCE_SPEAKER_ID)
                if cached is not None:
                    frontend.spk2info[REFERENCE_SPEAKER_ID] = self._move_nested_to_cpu(cached)

            self.reference_cache_ready = True
            LOG.info("Cached reference voice features for subsequent synthesis requests.")
        except Exception:
            # Caching is an optimization. Keep the service usable with the upstream direct path.
            self.reference_cache_ready = False
            LOG.exception("Reference voice precomputation failed; falling back to per-request extraction.")

    def _prepare_instruction_reference(self, instruction: str) -> str:
        if not self.reference_cache_ready or self.model is None:
            return ""

        frontend = getattr(self.model, "frontend", None)
        spk2info = getattr(frontend, "spk2info", None)
        extract_text = getattr(frontend, "_extract_text_token", None)
        if not isinstance(spk2info, dict) or not callable(extract_text):
            return ""

        base = spk2info.get(REFERENCE_SPEAKER_ID)
        if not isinstance(base, dict):
            return ""

        # Upstream's speaker-info cache pins prompt_text to the reference transcript.
        # For instruct2 we reuse only the acoustic/speaker features and replace prompt_text
        # with the current instruction, otherwise the style instruction can be ignored.
        prompt_text, prompt_text_len = extract_text(instruction)
        entry = dict(base)
        entry["prompt_text"] = prompt_text
        entry["prompt_text_len"] = prompt_text_len
        spk2info[INSTRUCTION_SPEAKER_ID] = entry
        return INSTRUCTION_SPEAKER_ID

    def _drop_instruction_reference(self, speaker_id: str) -> None:
        if self.model is None:
            return
        frontend = getattr(self.model, "frontend", None)
        spk2info = getattr(frontend, "spk2info", None)
        if isinstance(spk2info, dict):
            spk2info.pop(speaker_id, None)

    def _read_model_dtypes(self) -> dict[str, list[str]]:
        if self.model is None:
            return {}

        inner = getattr(self.model, "model", None)
        result: dict[str, list[str]] = {}
        for name in ("llm", "flow", "hift"):
            module = getattr(inner, name, None)
            if module is None or not hasattr(module, "parameters"):
                continue
            dtypes = sorted(
                {
                    str(parameter.dtype).removeprefix("torch.")
                    for parameter in module.parameters()
                    if parameter.is_floating_point()
                }
            )
            result[name] = dtypes
        return result

    def _verify_fp16_runtime(self, requested: bool) -> bool:
        if not requested:
            return False

        expected = {
            "llm": {"float16"},
            "flow": {"float32"},
            "hift": {"float32"},
        }
        mismatches = {
            name: self.model_dtypes.get(name, [])
            for name, dtypes in expected.items()
            if set(self.model_dtypes.get(name, [])) != dtypes
        }
        if mismatches:
            raise RuntimeError(
                "Low-VRAM mixed-precision profile did not load with the expected dtypes "
                f"(expected={expected!r}, actual={self.model_dtypes!r})."
            )

        return True

    @staticmethod
    def _validate_waveform(waveform: torch.Tensor) -> None:
        if waveform.numel() == 0:
            raise RuntimeError("CosyVoice3 produced an empty waveform.")

        finite = torch.isfinite(waveform)
        if not bool(finite.all()):
            bad = int((~finite).sum().item())
            raise RuntimeError(
                "CosyVoice3 produced non-finite audio samples "
                f"({bad}/{waveform.numel()}). Refusing to write a silent/corrupt WAV."
            )

        peak = float(waveform.abs().max().item())
        rms = float(torch.sqrt(torch.mean(waveform.square())).item())
        LOG.info("Generated waveform stats: samples=%s peak=%.6f rms=%.6f", waveform.numel(), peak, rms)

        # Keep the thresholds deliberately tiny: this is a corruption guard, not loudness
        # normalization. A valid quiet utterance remains far above these values.
        if peak < 1e-5 or rms < 1e-6:
            raise RuntimeError(
                "CosyVoice3 produced an effectively silent waveform "
                f"(peak={peak:.3e}, rms={rms:.3e}). Refusing to write a silent WAV."
            )

    def _read_speech_tokenizer_providers(self) -> list[str]:
        if self.model is None:
            return []
        session = getattr(
            getattr(self.model, "frontend", None),
            "speech_tokenizer_session",
            None,
        )
        if session is None or not hasattr(session, "get_providers"):
            return []
        try:
            return list(session.get_providers())
        except Exception:
            return []

    def _format_instruction(self, raw_instruction: str) -> str:
        """Format CosyVoice3 instruct2 context without letting control text leak into speech."""

        instruction = raw_instruction.strip()
        if not instruction:
            return ""

        # In CosyVoice3 instruct2, <|endofprompt|> belongs at the END of the instruction
        # context. Text placed after that delimiter can be treated as synthesis content.
        instruction = instruction.replace(END_OF_PROMPT, " ").strip()
        if instruction.startswith(ASSISTANT_PROMPT):
            instruction = instruction[len(ASSISTANT_PROMPT):].strip()

        return f"{ASSISTANT_PROMPT} {instruction}{END_OF_PROMPT}"

    def _load_prompt_text(self) -> str:
        if self.voice_source == "custom-reference" and self.reference_text.exists():
            raw = self.reference_text.read_text(encoding="utf-8").strip()
        else:
            raw = OFFICIAL_DEMO_PROMPT_TEXT

        # CosyVoice3 zero-shot expects the helper prompt + delimiter first, followed by
        # the exact transcript of the reference audio.
        raw = raw.replace(END_OF_PROMPT, " ").strip()
        if raw.startswith(ASSISTANT_PROMPT):
            raw = raw[len(ASSISTANT_PROMPT):].strip()

        return f"{ASSISTANT_PROMPT}{END_OF_PROMPT}{raw}"

    @staticmethod
    def _move_nested_to_cpu(value: Any) -> Any:
        if isinstance(value, torch.Tensor):
            return value.detach().cpu()
        if isinstance(value, dict):
            return {key: CosyVoiceRuntime._move_nested_to_cpu(item) for key, item in value.items()}
        if isinstance(value, list):
            return [CosyVoiceRuntime._move_nested_to_cpu(item) for item in value]
        if isinstance(value, tuple):
            return tuple(CosyVoiceRuntime._move_nested_to_cpu(item) for item in value)
        return value

    @staticmethod
    def _reset_cuda_peak_memory() -> None:
        if not torch.cuda.is_available():
            return
        try:
            torch.cuda.reset_peak_memory_stats()
        except Exception:
            pass

    def _log_cuda_memory(self, stage: str) -> None:
        snapshot = self.gpu_memory_snapshot()
        if snapshot is None:
            return
        LOG.info(
            "CUDA memory %s: allocated=%s MiB reserved=%s MiB peak=%s MiB free=%s MiB / %s MiB",
            stage,
            snapshot["allocatedMiB"],
            snapshot["reservedMiB"],
            snapshot["peakAllocatedMiB"],
            snapshot["freeMiB"],
            snapshot["totalMiB"],
        )


def create_app(runtime: CosyVoiceRuntime) -> FastAPI:
    """Create the local-only HTTP service used by the C# backend."""

    app = FastAPI(title="AI Avatar CosyVoice3", docs_url=None, redoc_url=None)

    @app.get("/health")
    def health() -> dict[str, object]:
        return {
            "service": SERVICE_ID,
            "contractVersion": HEALTH_CONTRACT_VERSION,
            "ready": runtime.is_ready(),
            "model": runtime.model_dir.name,
            "modelPath": str(runtime.model_dir.resolve()),
            "sampleRate": runtime.sample_rate,
            "voiceSource": runtime.voice_source,
            "referenceWavPath": str(runtime.reference_wav.resolve()),
            "referenceTextPath": (
                str(runtime.reference_text.resolve())
                if runtime.voice_source == "custom-reference"
                else None
            ),
            "cudaAvailable": torch.cuda.is_available(),
            "fp16": runtime.fp16_enabled,
            "precisionProfile": runtime.precision_profile,
            "modelDtypes": runtime.model_dtypes,
            "lowVramMode": runtime.low_vram_mode,
            "referenceCacheReady": runtime.reference_cache_ready,
            "speechTokenizerProviders": runtime.speech_tokenizer_providers,
            "gpuMemory": runtime.gpu_memory_snapshot(),
        }

    @app.post("/synthesize")
    def synthesize(request: SynthesisRequest) -> Response:
        if not runtime.is_ready():
            raise HTTPException(status_code=503, detail="CosyVoice3 is not ready.")

        try:
            audio = runtime.synthesize(request)
        except ValueError as exception:
            raise HTTPException(status_code=400, detail=str(exception)) from exception
        except Exception as exception:
            LOG.exception("CosyVoice3 synthesis failed")
            raise HTTPException(status_code=500, detail=str(exception)) from exception

        return Response(
            content=audio,
            media_type="audio/wav",
            headers={
                "X-CosyVoice-Model": runtime.model_dir.name,
                "X-CosyVoice-Voice-Source": runtime.voice_source,
                "X-CosyVoice-FP16": str(runtime.fp16_enabled).lower(),
                "X-CosyVoice-Precision-Profile": runtime.precision_profile,
                "X-CosyVoice-Low-VRAM": str(runtime.low_vram_mode).lower(),
            },
        )

    return app


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Local CosyVoice3 avatar TTS service")
    parser.add_argument("--cosyvoice-repo", type=Path, required=True)
    parser.add_argument("--model-dir", type=Path, required=True)
    parser.add_argument("--reference-wav", type=Path, required=True)
    parser.add_argument("--reference-text", type=Path, required=True)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8188)
    parser.add_argument("--use-official-demo-voice", action="store_true")
    parser.add_argument(
        "--low-vram",
        action="store_true",
        help=(
            "Enable the 4 GB VRAM profile: LLM CUDA FP16, Flow FP32 sequential GPU "
            "offload, HiFT CPU FP32, CPU speech tokenizer, and inference_mode."
        ),
    )
    return parser.parse_args()


def main() -> None:
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
    )
    args = parse_args()

    runtime = CosyVoiceRuntime(
        cosyvoice_repo=args.cosyvoice_repo.resolve(),
        model_dir=args.model_dir.resolve(),
        reference_wav=args.reference_wav.resolve(),
        reference_text=args.reference_text.resolve(),
        use_official_demo_voice=args.use_official_demo_voice,
        low_vram_mode=args.low_vram,
    )
    runtime.load()

    uvicorn.run(
        create_app(runtime),
        host=args.host,
        port=args.port,
        log_level="info",
    )


if __name__ == "__main__":
    main()
