using System.Text.Json;
using AiAvatar.Backend.Options;
using AiAvatar.Backend.Services.Ollama;

namespace AiAvatar.Backend.Tests;

public sealed class OllamaResponseParserTests
{
    [Fact]
    public void Parse_ValidStructuredDecisionSanitizesAndReadsTelemetry()
    {
        var parser = CreateParser();
        var decisionJson = JsonSerializer.Serialize(new
        {
            speech = "  hello  ",
            language = "EN-US",
            emotion = "HAPPY",
            emotionIntensity = 0.7,
            gesture = "nod",
            gestureIntensity = 0.2,
        });
        var envelope = JsonSerializer.Serialize(new
        {
            model = "test-model",
            total_duration = 1_234_567L,
            load_duration = 500_000L,
            prompt_eval_count = 12,
            eval_count = 7,
            message = new { content = decisionJson },
        });

        var result = parser.Parse(envelope);

        Assert.Equal("hello", result.Decision.Speech);
        Assert.Equal("en", result.Decision.Language);
        Assert.Equal("happy", result.Decision.Emotion);
        Assert.Equal("nod", result.Decision.Gesture);
        Assert.Equal(0.2, result.Decision.GestureIntensity);
        Assert.Equal("test-model", result.Telemetry.Model);
        Assert.Equal(1.2, result.Telemetry.TotalDurationMs!.Value);
        Assert.Equal(0.5, result.Telemetry.LoadDurationMs!.Value);
        Assert.Equal(12, result.Telemetry.PromptTokens!.Value);
        Assert.Equal(7, result.Telemetry.OutputTokens!.Value);
    }

    [Fact]
    public void Parse_InvalidAvatarJsonReturnsSafeFallbackAndPreservesTelemetry()
    {
        var parser = CreateParser();
        var envelope = JsonSerializer.Serialize(new
        {
            total_duration = 2_000_000L,
            message = new { content = "not-json" },
        });

        var result = parser.Parse(envelope);

        Assert.True(result.Decision.Speech.Contains("avatar-control JSON was invalid", StringComparison.Ordinal));
        Assert.Equal("en", result.Decision.Language);
        Assert.Equal("confused", result.Decision.Emotion);
        Assert.Equal("none", result.Decision.Gesture);
        Assert.Equal("configured-model", result.Telemetry.Model);
        Assert.Equal(2.0, result.Telemetry.TotalDurationMs!.Value);
    }

    [Fact]
    public void Parse_EmptyContentReturnsSafeFallback()
    {
        var parser = CreateParser();
        var envelope = JsonSerializer.Serialize(new
        {
            message = new { content = "   " },
        });

        var result = parser.Parse(envelope);

        Assert.True(result.Decision.Speech.Contains("empty response", StringComparison.Ordinal));
        Assert.Equal("confused", result.Decision.Emotion);
    }

    private static OllamaResponseParser CreateParser() =>
        new(Microsoft.Extensions.Options.Options.Create(new OllamaOptions { Model = "configured-model" }));
}
