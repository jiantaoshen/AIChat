"""Run a local FastAPI wrapper around the official CosyVoice3 model for avatar text-to-speech synthesis."""

from __future__ import annotations

import argparse
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
    ) -> None:
        self.cosyvoice_repo = cosyvoice_repo
        self.model_dir = model_dir
        self.reference_wav = reference_wav
        self.reference_text = reference_text
        self.use_official_demo_voice = use_official_demo_voice
        self.model: Any | None = None
        self.sample_rate: int | None = None
        self.voice_source = "unconfigured"
        self._lock = threading.Lock()

    def load(self) -> None:
        self._configure_import_paths()
        self._resolve_reference_voice()

        from cosyvoice.cli.cosyvoice import AutoModel  # type: ignore

        LOG.info("Loading CosyVoice3 model from %s", self.model_dir)
        LOG.info("CUDA available: %s", torch.cuda.is_available())

        # Keep the default path conservative on Windows. TensorRT/vLLM/JIT acceleration can be
        # enabled later without changing the HTTP contract used by the C# backend.
        # CosyVoice3 does not accept the legacy CosyVoice2 `load_jit` option.
        # Keep Windows startup conservative: no TensorRT/vLLM, FP32 by default.
        self.model = AutoModel(
            model_dir=str(self.model_dir),
            load_trt=False,
            load_vllm=False,
            fp16=False,
        )
        self.sample_rate = int(self.model.sample_rate)

        LOG.info(
            "CosyVoice3 ready at %s Hz using voice source '%s'.",
            self.sample_rate,
            self.voice_source,
        )

    def synthesize(self, request: SynthesisRequest) -> bytes:
        if self.model is None or self.sample_rate is None:
            raise RuntimeError("CosyVoice3 model has not been loaded.")

        text = request.text.strip()
        if not text:
            raise ValueError("Text cannot be empty.")

        instruction = self._format_instruction(request.instruction)

        with self._lock:
            if instruction:
                generator = self.model.inference_instruct2(
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
                    stream=False,
                    speed=request.speed,
                )

            chunks: list[torch.Tensor] = []
            for item in generator:
                speech = item.get("tts_speech")
                if speech is None:
                    continue
                chunks.append(speech.detach().to("cpu"))

        if not chunks:
            raise RuntimeError("CosyVoice3 produced no audio samples.")

        waveform = torch.cat(chunks, dim=1).squeeze(0).float().numpy()
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

    def _format_instruction(self, raw_instruction: str) -> str:
        """Format CosyVoice3 instruct2 context without letting control text leak into speech."""

        instruction = raw_instruction.strip()
        if not instruction:
            return ""

        # Accept payloads from older project builds safely. In CosyVoice3 instruct2,
        # <|endofprompt|> belongs at the END of the instruction context. Text placed
        # after that delimiter can be treated as synthesis content and spoken aloud.
        instruction = instruction.replace(END_OF_PROMPT, " ").strip()
        if instruction.startswith(ASSISTANT_PROMPT):
            instruction = instruction[len(ASSISTANT_PROMPT):].strip()

        return f"{ASSISTANT_PROMPT} {instruction}{END_OF_PROMPT}"

    def _load_prompt_text(self) -> str:
        if self.voice_source == "custom-reference" and self.reference_text.exists():
            raw = self.reference_text.read_text(encoding="utf-8").strip()
        else:
            raw = OFFICIAL_DEMO_PROMPT_TEXT

        # CosyVoice3 zero-shot expects the helper prompt + delimiter FIRST, followed
        # by the exact transcript of the reference audio. Keep model control tokens
        # out of reference.txt so that file remains human-readable.
        raw = raw.replace(END_OF_PROMPT, " ").strip()
        if raw.startswith(ASSISTANT_PROMPT):
            raw = raw[len(ASSISTANT_PROMPT):].strip()

        return f"{ASSISTANT_PROMPT}{END_OF_PROMPT}{raw}"


def create_app(runtime: CosyVoiceRuntime) -> FastAPI:
    """Create the local-only HTTP service used by the C# backend."""

    app = FastAPI(title="AI Avatar CosyVoice3", docs_url=None, redoc_url=None)

    @app.get("/health")
    def health() -> dict[str, object]:
        return {
            "ready": runtime.is_ready(),
            "model": runtime.model_dir.name,
            "sampleRate": runtime.sample_rate,
            "voiceSource": runtime.voice_source,
            "cudaAvailable": torch.cuda.is_available(),
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
