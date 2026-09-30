// This file defines the browser-to-backend request used to synthesize one persisted assistant message with validated semantic emotion data.
namespace AiAvatar.Backend.Models;

public sealed record SpeechSynthesisRequest(
    Guid MessageId,
    string Text,
    string Emotion,
    double EmotionIntensity);
