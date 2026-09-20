// This file sanitizes Qwen output so only supported expressions, gestures, text, and safe intensity values reach the renderer.
using AiAvatar.Backend.Models;

namespace AiAvatar.Backend.Services;

public static class AvatarDecisionValidator
{
    private static readonly HashSet<string> AllowedEmotions =
    [
        "neutral",
        "happy",
        "sad",
        "angry",
        "surprised",
        "confused"
    ];

    private static readonly HashSet<string> AllowedGestures =
    [
        "none",
        "nod",
        "shake",
        "jump"
    ];

    public static AvatarDecision Sanitize(AvatarDecision? decision)
    {
        if (decision is null)
        {
            return Fallback("I could not build a response just now.");
        }

        var speech = string.IsNullOrWhiteSpace(decision.Speech)
            ? "I am not sure how to answer that yet."
            : decision.Speech.Trim();

        if (speech.Length > 1_200)
        {
            speech = $"{speech[..1_200].TrimEnd()}…";
        }

        return new AvatarDecision(
            speech,
            NormalizeChoice(decision.Emotion, AllowedEmotions, "neutral"),
            Clamp01(decision.EmotionIntensity),
            NormalizeChoice(decision.Gesture, AllowedGestures, "none"),
            Clamp01(decision.GestureIntensity));
    }

    public static AvatarDecision Fallback(string message) =>
        new(
            message,
            "confused",
            0.45,
            "none",
            0.0);

    private static string NormalizeChoice(
        string? value,
        HashSet<string> allowed,
        string fallback)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized is not null && allowed.Contains(normalized)
            ? normalized
            : fallback;
    }

    private static double Clamp01(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 0;
        }

        return Math.Clamp(value, 0, 1);
    }
}
