// This file defines the abstract semantic avatar command returned by Qwen before the frontend renders it.
namespace AiAvatar.Backend.Models;

public sealed record AvatarDecision(
    string Speech,
    string Language,
    string Emotion,
    double EmotionIntensity,
    string Gesture,
    double GestureIntensity);
