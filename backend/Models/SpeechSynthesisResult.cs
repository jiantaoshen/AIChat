// This file carries successful CosyVoice3 audio plus runtime telemetry back to the API layer.
namespace AiAvatar.Backend.Models;

public sealed record SpeechSynthesisResult(
    byte[] Audio,
    long SynthesisDurationMs,
    string Model,
    string? VoiceSource,
    bool? UsedCuda,
    bool UsedFp16);
