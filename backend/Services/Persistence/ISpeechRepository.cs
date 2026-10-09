// This interface owns authoritative assistant speech lookup and persisted TTS telemetry.
using AiAvatar.Backend.Models;

namespace AiAvatar.Backend.Services.Persistence;

public interface ISpeechRepository
{
    Task<AssistantSpeechSource?> GetAssistantSpeechSourceAsync(
        Guid messageId,
        CancellationToken cancellationToken);

    Task SaveTtsTelemetryAsync(
        Guid messageId,
        TtsTelemetryRecord telemetry,
        CancellationToken cancellationToken);
}
