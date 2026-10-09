// This file maps backend-owned semantic avatar emotion into bounded CosyVoice instructions and speaking speed so the client never controls low-level TTS parameters.
using AiAvatar.Backend.Models;

namespace AiAvatar.Backend.Services.Speech;

public static class TtsSpeechPolicy
{
    public static CosyVoiceSynthesisPayload CreatePayload(SpeechSynthesisInput input)
    {
        var text = input.Text.Trim();
        var emotion = NormalizeEmotion(input.Emotion);
        var intensity = Clamp01(input.EmotionIntensity);

        var (instruction, baseSpeed, speedRange) = emotion switch
        {
            "happy" => (
                "Speak warmly and naturally, with a gentle cheerful tone. Do not exaggerate the emotion.",
                1.02,
                0.05),
            "sad" => (
                "Speak softly and calmly with restrained sadness. Keep the delivery natural and not theatrical.",
                0.95,
                -0.04),
            "angry" => (
                "Speak with controlled firmness and restrained irritation. Do not shout.",
                1.00,
                0.04),
            "surprised" => (
                "Speak with mild elegant surprise, slightly brighter and quicker, without becoming comedic.",
                1.03,
                0.04),
            "confused" => (
                "Speak thoughtfully with mild uncertainty and a natural questioning tone.",
                0.98,
                -0.02),
            _ => (
                "Speak in a calm, natural, clear conversational voice.",
                1.00,
                0.00),
        };

        var speed = Math.Clamp(baseSpeed + speedRange * intensity, 0.88, 1.12);

        return new CosyVoiceSynthesisPayload(text, instruction, speed);
    }

    public static bool IsSupportedEmotion(string? emotion)
    {
        if (string.IsNullOrWhiteSpace(emotion))
        {
            return false;
        }

        return emotion.Trim().ToLowerInvariant() is
            "neutral" or "happy" or "sad" or "angry" or "surprised" or "confused";
    }

    private static string NormalizeEmotion(string? emotion)
    {
        var normalized = emotion?.Trim().ToLowerInvariant();
        return IsSupportedEmotion(normalized) ? normalized! : "neutral";
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
