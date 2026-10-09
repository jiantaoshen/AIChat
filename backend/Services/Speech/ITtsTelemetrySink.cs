using AiAvatar.Backend.Models;

namespace AiAvatar.Backend.Services.Speech;

// Non-blocking observability boundary. Returning false means telemetry was not
// accepted, but it must never change the already-successful synthesis result.
public interface ITtsTelemetrySink
{
    bool TryRecord(Guid messageId, TtsTelemetryRecord telemetry);
}
