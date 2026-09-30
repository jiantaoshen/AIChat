// This file carries one validated Qwen avatar decision and Ollama telemetry inside the backend before persistence identifiers are attached.
namespace AiAvatar.Backend.Models;

public sealed record OllamaDecisionResult(
    AvatarDecision Decision,
    ModelTelemetry Telemetry);
