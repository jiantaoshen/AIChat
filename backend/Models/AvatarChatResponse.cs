// This file defines the complete browser API response: a validated avatar decision plus local inference telemetry.
namespace AiAvatar.Backend.Models;

public sealed record AvatarChatResponse(
    AvatarDecision Decision,
    ModelTelemetry Telemetry);
