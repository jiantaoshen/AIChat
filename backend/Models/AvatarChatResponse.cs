// This file defines the complete browser API response: persistence identifiers, a validated avatar decision, local inference telemetry, and backend-owned speech capability.
namespace AiAvatar.Backend.Models;

public sealed record AvatarChatResponse(
    Guid ConversationId,
    Guid AssistantMessageId,
    AvatarDecision Decision,
    ModelTelemetry Telemetry)
{
    // ChatTurnService sets this from TtsSpeechPolicy. Default is deliberately fail-closed.
    public SpeechCapability SpeechCapability { get; init; } = new(false);
}
