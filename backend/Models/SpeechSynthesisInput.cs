// This file carries validated server-owned speech semantics from persistence into the deterministic TTS policy.
namespace AiAvatar.Backend.Models;

public sealed record SpeechSynthesisInput(
    string Text,
    string Language,
    string Emotion,
    double EmotionIntensity);
