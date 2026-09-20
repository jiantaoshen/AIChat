// This file defines lightweight Ollama timing and token metrics displayed by the frontend for local-model experiments.
namespace AiAvatar.Backend.Models;

public sealed record ModelTelemetry(
    string Model,
    double? TotalDurationMs,
    double? LoadDurationMs,
    int? PromptTokens,
    int? OutputTokens);
