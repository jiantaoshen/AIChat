// This file defines the browser-to-backend request used to synthesize avatar speech from validated semantic emotion data.
namespace AiAvatar.Backend.Models;

public sealed record SpeechSynthesisRequest(
    string Text,
    string Emotion,
    double EmotionIntensity);
