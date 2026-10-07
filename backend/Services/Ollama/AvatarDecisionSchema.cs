// This file defines the JSON Schema sent to Ollama so Qwen can only return the supported speech, language, emotion, gesture, and intensity fields.
namespace AiAvatar.Backend.Services.Ollama;

public static class AvatarDecisionSchema
{
    public static object Value { get; } = new
    {
        type = "object",
        properties = new
        {
            speech = new
            {
                type = "string",
                description = "The short natural-language reply spoken to the user."
            },
            language = new
            {
                type = "string",
                description = "The lowercase ISO 639-1 language code of the speech reply, for example zh, en, sv, ja, de, or fr. Use und only when genuinely uncertain."
            },
            emotion = new
            {
                type = "string",
                @enum = new[] { "neutral", "happy", "sad", "angry", "surprised", "confused" }
            },
            emotionIntensity = new
            {
                type = "number",
                minimum = 0.0,
                maximum = 1.0
            },
            gesture = new
            {
                type = "string",
                @enum = new[] { "none", "nod", "shake", "jump" }
            },
            gestureIntensity = new
            {
                type = "number",
                minimum = 0.0,
                maximum = 1.0
            }
        },
        required = new[]
        {
            "speech",
            "language",
            "emotion",
            "emotionIntensity",
            "gesture",
            "gestureIntensity"
        },
        additionalProperties = false
    };
}
