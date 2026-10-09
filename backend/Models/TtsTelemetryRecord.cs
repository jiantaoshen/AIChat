// Small immutable observability payload queued after successful TTS synthesis.
// Audio bytes are intentionally excluded so telemetry buffering never retains WAV data.
namespace AiAvatar.Backend.Models;

public sealed record TtsTelemetryRecord(
    long SynthesisDurationMs,
    string Model,
    string? VoiceSource,
    bool? UsedCuda,
    bool UsedFp16)
{
    public static TtsTelemetryRecord FromResult(SpeechSynthesisResult result) =>
        new(
            result.SynthesisDurationMs,
            result.Model,
            result.VoiceSource,
            result.UsedCuda,
            result.UsedFp16);
}
