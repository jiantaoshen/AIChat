// This file defines the complete browser API response: persistence identifiers, a validated avatar decision, and local inference telemetry.
namespace AiAvatar.Backend.Models;

public sealed record AvatarChatResponse(
    Guid ConversationId,
    Guid AssistantMessageId,
    AvatarDecision Decision,
    ModelTelemetry Telemetry);
