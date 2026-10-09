// This file carries the persisted assistant fields that are authoritative for one TTS operation.
namespace AiAvatar.Backend.Models;

public sealed record AssistantSpeechSource(
    string Text,
    string? Language,
    string? Emotion,
    double? EmotionIntensity);
