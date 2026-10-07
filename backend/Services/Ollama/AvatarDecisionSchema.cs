// This file defines the JSON Schema sent to Ollama so Qwen can only return the supported speech, emotion, gesture, and intensity fields.
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
            "emotion",
            "emotionIntensity",
            "gesture",
            "gestureIntensity"
        },
        additionalProperties = false
    };
}
