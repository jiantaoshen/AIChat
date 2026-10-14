"""Low-VRAM runtime overrides for the upstream CosyVoice inference classes.

The 4 GB profile intentionally uses mixed precision and sequential module offload:

* speech tokenizer + reference preprocessing: CPU
* CosyVoice LLM: CUDA FP16 while generating speech tokens
* Flow decoder: CUDA FP32 only while converting tokens -> mel
* HiFT vocoder: CPU FP32

This avoids the silent/NaN audio failure seen when CosyVoice3 Flow/HiFT are forced
through FP16/autocast, while still keeping peak CUDA residency suitable for a 4 GB GPU.
"""

from __future__ import annotations

import inspect
import logging
from contextlib import nullcontext
from pathlib import Path
from typing import Any

import torch
import torch.nn.functional as F

LOG = logging.getLogger("ai-avatar-cosyvoice.low-vram")
_PATCH_MARKER = "_ai_avatar_low_vram_patch"


def install_low_vram_overrides() -> None:
    """Install the 4 GB-safe CosyVoice3 runtime overrides.

    Important: ``fp16=True`` in upstream CosyVoice3 does more than make the LLM
    cheaper. It also wraps Flow + HiFT decoding in CUDA autocast. On some current
    CosyVoice3 checkpoints/hardware combinations that path can produce NaN audio,
    which becomes a valid-looking but silent PCM16 WAV when written to disk.

    We therefore keep FP16 only where it is useful and numerically safe for this
    project: the autoregressive LLM. Flow/HiFT remain FP32, with Flow swapped onto
    CUDA only for the non-streaming decoder pass and HiFT kept on CPU.
    """

    from cosyvoice.cli.frontend import CosyVoiceFrontEnd  # type: ignore
    from cosyvoice.cli.model import (  # type: ignore
        CosyVoice2Model,
        CosyVoice3Model,
        CosyVoiceModel,
    )

    _patch_frontend_init(CosyVoiceFrontEnd)

    # Current CosyVoice2/3 inherit the base checkpoint loader. Patch the base loader
    # and any subclass that starts overriding it in a future upstream revision.
    for model_cls in (CosyVoiceModel, CosyVoice2Model, CosyVoice3Model):
        if model_cls is CosyVoiceModel or "load" in model_cls.__dict__:
            _patch_model_load(model_cls)

    # CosyVoice3 has a distinct token2wav implementation. Replace only that decoder
    # path so LLM autocast can stay enabled while Flow/HiFT decoding stays FP32.
    _patch_cosyvoice3_token2wav(CosyVoice3Model)


def _patch_frontend_init(frontend_cls: type[Any]) -> None:
    if getattr(frontend_cls.__init__, _PATCH_MARKER, False):
        return

    original_init = frontend_cls.__init__

    def low_vram_init(self: Any, *args: Any, **kwargs: Any) -> None:
        # Keep the patch narrowly scoped to frontend construction. The model itself
        # must still see CUDA so its LLM can use the GPU.
        import onnxruntime  # type: ignore

        original_session = onnxruntime.InferenceSession

        def cpu_speech_tokenizer_session(
            path_or_bytes: Any,
            *session_args: Any,
            **session_kwargs: Any,
        ) -> Any:
            try:
                name = Path(str(path_or_bytes)).name.lower()
            except Exception:
                name = ""

            if name.startswith("speech_tokenizer"):
                session_kwargs["providers"] = ["CPUExecutionProvider"]

            return original_session(path_or_bytes, *session_args, **session_kwargs)

        onnxruntime.InferenceSession = cpu_speech_tokenizer_session
        try:
            original_init(self, *args, **kwargs)
        finally:
            onnxruntime.InferenceSession = original_session

        # Frontend token/features are cheap to keep in RAM. The model inference code
        # explicitly moves the required inputs to its execution device.
        self.device = torch.device("cpu")
        if hasattr(self, "spk2info"):
            self.spk2info = _move_nested_to_cpu(self.spk2info)

        providers: list[str] = []
        session = getattr(self, "speech_tokenizer_session", None)
        if session is not None and hasattr(session, "get_providers"):
            providers = list(session.get_providers())

        if "CPUExecutionProvider" not in providers:
            raise RuntimeError(
                "Low-VRAM mode requires the CosyVoice speech tokenizer to use the CPU "
                f"provider, but ONNX Runtime reported providers={providers!r}."
            )

        LOG.info("Speech tokenizer pinned to CPU provider(s): %s", providers)

    setattr(low_vram_init, _PATCH_MARKER, True)
    frontend_cls.__init__ = low_vram_init


def _patch_model_load(model_cls: type[Any]) -> None:
    if getattr(model_cls.load, _PATCH_MARKER, False):
        return

    original_load = model_cls.load
    try:
        parameters = inspect.signature(original_load).parameters
    except (TypeError, ValueError) as exception:
        raise RuntimeError(
            f"Cannot inspect {model_cls.__name__}.load; the upstream CosyVoice API changed."
        ) from exception

    required = {"self", "llm_model", "flow_model", "hift_model"}
    if not required.issubset(parameters):
        raise RuntimeError(
            f"Unsupported {model_cls.__name__}.load signature {tuple(parameters)}. "
            "Update tools/cosyvoice-service/low_vram.py for this upstream CosyVoice revision."
        )

    def low_vram_load(
        self: Any,
        llm_model: str,
        flow_model: str,
        hift_model: str,
    ) -> None:
        if not torch.cuda.is_available() or not bool(getattr(self, "fp16", False)):
            original_load(self, llm_model, flow_model, hift_model)
            return

        device = torch.device(getattr(self, "device", torch.device("cuda")))
        if device.type != "cuda":
            original_load(self, llm_model, flow_model, hift_model)
            return

        torch.cuda.empty_cache()
        LOG.info(
            "Using 4 GB mixed-precision loader: LLM=CUDA FP16, Flow=CPU FP32 (swapped on demand), HiFT=CPU FP32"
        )

        # Only the AR LLM is kept resident on the GPU. This saves about half of its
        # checkpoint memory without exposing Flow/HiFT to unstable half precision.
        _load_module(self.llm, llm_model, device=device, dtype=torch.float16, label="LLM")
        _load_module(self.flow, flow_model, device=torch.device("cpu"), dtype=torch.float32, label="Flow")
        _load_hift(self.hift, hift_model, device=torch.device("cpu"), dtype=torch.float32)

        torch.cuda.empty_cache()
        _log_cuda_memory("after mixed-precision model load")

    setattr(low_vram_load, _PATCH_MARKER, True)
    model_cls.load = low_vram_load


def _patch_cosyvoice3_token2wav(model_cls: type[Any]) -> None:
    original = model_cls.token2wav
    if getattr(original, _PATCH_MARKER, False):
        return

    try:
        parameters = inspect.signature(original).parameters
    except (TypeError, ValueError) as exception:
        raise RuntimeError(
            "Cannot inspect CosyVoice3Model.token2wav; the upstream CosyVoice API changed."
        ) from exception

    required = {
        "self",
        "token",
        "prompt_token",
        "prompt_feat",
        "embedding",
        "token_offset",
        "uuid",
        "stream",
        "finalize",
        "speed",
    }
    if not required.issubset(parameters):
        raise RuntimeError(
            f"Unsupported CosyVoice3Model.token2wav signature {tuple(parameters)}. "
            "Update tools/cosyvoice-service/low_vram.py for this upstream CosyVoice revision."
        )

    def stable_token2wav(
        self: Any,
        token: torch.Tensor,
        prompt_token: torch.Tensor,
        prompt_feat: torch.Tensor,
        embedding: torch.Tensor,
        token_offset: int,
        uuid: str,
        stream: bool = False,
        finalize: bool = False,
        speed: float = 1.0,
    ) -> torch.Tensor:
        # The sequential 4 GB profile is intentionally non-streaming for now. During
        # streaming, Flow decode overlaps with the LLM worker, so swapping the LLM off
        # CUDA would race with token generation. The HTTP service currently calls the
        # official API with stream=False, so fail loudly if that contract changes.
        if stream:
            raise RuntimeError(
                "The 4 GB low-VRAM profile currently supports CosyVoice stream=False only. "
                "Use the dedicated streaming scheduler before enabling stream=True."
            )

        cuda_device = torch.device(getattr(self, "device", "cuda"))
        if cuda_device.type != "cuda":
            return original(
                self,
                token,
                prompt_token,
                prompt_feat,
                embedding,
                token_offset,
                uuid,
                stream=stream,
                finalize=finalize,
                speed=speed,
            )

        # For non-stream inference, upstream joins the LLM thread before token2wav.
        # That lets us reclaim the ~1 GB FP16 LLM residency before bringing the FP32
        # Flow decoder onto a 4 GB GPU.
        torch.cuda.synchronize(cuda_device)
        self.llm.to(device="cpu", dtype=torch.float16)
        torch.cuda.empty_cache()

        try:
            try:
                self.flow.to(device=cuda_device, dtype=torch.float32).eval()
                torch.cuda.empty_cache()
                _log_cuda_memory("after swapping Flow onto CUDA")

                # Explicitly disable autocast for the numerically sensitive decoder.
                precision_context = (
                    torch.cuda.amp.autocast(enabled=False)
                    if torch.cuda.is_available()
                    else nullcontext()
                )
                with precision_context:
                    tts_mel, _ = self.flow.inference(
                        token=token.to(cuda_device, dtype=torch.int32),
                        token_len=torch.tensor([token.shape[1]], dtype=torch.int32, device=cuda_device),
                        prompt_token=prompt_token.to(cuda_device),
                        prompt_token_len=torch.tensor(
                            [prompt_token.shape[1]], dtype=torch.int32, device=cuda_device
                        ),
                        prompt_feat=prompt_feat.to(cuda_device, dtype=torch.float32),
                        prompt_feat_len=torch.tensor(
                            [prompt_feat.shape[1]], dtype=torch.int32, device=cuda_device
                        ),
                        embedding=embedding.to(cuda_device, dtype=torch.float32),
                        streaming=False,
                        finalize=finalize,
                    )

                    tts_mel = tts_mel[:, :, token_offset * self.flow.token_mel_ratio :].float()

                # Flow no longer needs GPU memory. Move the mel to RAM before offloading it.
                tts_mel = tts_mel.detach().cpu().float()
            finally:
                self.flow.to(device="cpu", dtype=torch.float32)
                torch.cuda.empty_cache()

            # HiFT stays FP32 on CPU. It is much smaller than Flow and this keeps the CUDA
            # peak deterministic while avoiding the half-precision vocoder failure mode.
            cache = self.hift_cache_dict.get(uuid)
            if cache is not None:
                hift_cache_mel = cache["mel"].detach().cpu().float()
                tts_mel = torch.concat([hift_cache_mel, tts_mel], dim=2)
                cache["mel"] = tts_mel
            else:
                self.hift_cache_dict[uuid] = {"mel": tts_mel, "speech_offset": 0}
                cache = self.hift_cache_dict[uuid]

            if speed != 1.0:
                if token_offset != 0 or not finalize:
                    raise AssertionError("speed change only support non-stream inference mode")
                tts_mel = F.interpolate(
                    tts_mel,
                    size=int(tts_mel.shape[2] / speed),
                    mode="linear",
                )

            self.hift.to(device="cpu", dtype=torch.float32).eval()
            with torch.inference_mode():
                tts_speech, _ = self.hift.inference(speech_feat=tts_mel, finalize=finalize)

            speech_offset = int(cache["speech_offset"])
            tts_speech = tts_speech[:, speech_offset:]
            cache["speech_offset"] = speech_offset + int(tts_speech.shape[1])
            return tts_speech
        finally:
            # Always restore the idle layout, even after CUDA OOM / decoder exceptions,
            # so the next request cannot inherit a half-swapped model.
            try:
                self.flow.to(device="cpu", dtype=torch.float32)
                self.llm.to(device=cuda_device, dtype=torch.float16).eval()
            finally:
                torch.cuda.empty_cache()
            _log_cuda_memory("after restoring LLM on CUDA")

    setattr(stable_token2wav, _PATCH_MARKER, True)
    model_cls.token2wav = stable_token2wav


def _load_module(
    module: torch.nn.Module,
    checkpoint_path: str,
    *,
    device: torch.device,
    dtype: torch.dtype,
    label: str,
) -> None:
    LOG.info("Loading %s checkpoint on CPU: %s", label, checkpoint_path)
    state = torch.load(checkpoint_path, map_location="cpu", weights_only=True)
    try:
        module.load_state_dict(state, strict=True)
    finally:
        del state

    module.to(device=device, dtype=dtype).eval()
    module.requires_grad_(False)
    if torch.cuda.is_available():
        torch.cuda.empty_cache()
    _log_cuda_memory(f"after {label} load")


def _load_hift(
    module: torch.nn.Module,
    checkpoint_path: str,
    *,
    device: torch.device,
    dtype: torch.dtype,
) -> None:
    LOG.info("Loading HiFT checkpoint on CPU: %s", checkpoint_path)
    raw_state = torch.load(checkpoint_path, map_location="cpu", weights_only=True)
    try:
        state = {key.replace("generator.", ""): value for key, value in raw_state.items()}
        module.load_state_dict(state, strict=True)
    finally:
        del raw_state
        if "state" in locals():
            del state

    module.to(device=device, dtype=dtype).eval()
    module.requires_grad_(False)
    if torch.cuda.is_available():
        torch.cuda.empty_cache()
    _log_cuda_memory("after HiFT load")


def _move_nested_to_cpu(value: Any) -> Any:
    if isinstance(value, torch.Tensor):
        return value.detach().cpu()
    if isinstance(value, dict):
        return {key: _move_nested_to_cpu(item) for key, item in value.items()}
    if isinstance(value, list):
        return [_move_nested_to_cpu(item) for item in value]
    if isinstance(value, tuple):
        return tuple(_move_nested_to_cpu(item) for item in value)
    return value


def _log_cuda_memory(stage: str) -> None:
    if not torch.cuda.is_available():
        return

    mib = 1024 * 1024
    LOG.info(
        "CUDA memory %s: allocated=%.1f MiB reserved=%.1f MiB peak=%.1f MiB",
        stage,
        torch.cuda.memory_allocated() / mib,
        torch.cuda.memory_reserved() / mib,
        torch.cuda.max_memory_allocated() / mib,
    )
