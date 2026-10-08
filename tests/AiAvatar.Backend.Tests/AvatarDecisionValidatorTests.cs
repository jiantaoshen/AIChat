using AiAvatar.Backend.Models;
using AiAvatar.Backend.Services;

namespace AiAvatar.Backend.Tests;

public sealed class AvatarDecisionValidatorTests
{
    [Fact]
    public void Sanitize_NormalizesLanguageChoicesAndIntensities()
    {
        var raw = new AvatarDecision(
            "  Hello there.  ",
            "EN_us",
            " HAPPY ",
            1.8,
            "SHAKE",
            -0.2);

        var result = AvatarDecisionValidator.Sanitize(raw);

        Assert.Equal("Hello there.", result.Speech);
        Assert.Equal("en", result.Language);
        Assert.Equal("happy", result.Emotion);
        Assert.Equal(1.0, result.EmotionIntensity);
        Assert.Equal("shake", result.Gesture);
        Assert.Equal(0.0, result.GestureIntensity);
    }

    [Fact]
    public void Sanitize_InvalidSemanticValuesFallBackSafely()
    {
        var raw = new AvatarDecision(
            "answer",
            "english",
            "ecstatic",
            double.NaN,
            "teleport",
            double.PositiveInfinity);

        var result = AvatarDecisionValidator.Sanitize(raw);

        Assert.Equal("und", result.Language);
        Assert.Equal("neutral", result.Emotion);
        Assert.Equal(0.0, result.EmotionIntensity);
        Assert.Equal("none", result.Gesture);
        Assert.Equal(0.0, result.GestureIntensity);
    }

    [Fact]
    public void Sanitize_NullDecisionReturnsStableFallback()
    {
        var result = AvatarDecisionValidator.Sanitize(null);

        Assert.Equal("I could not build a response just now.", result.Speech);
        Assert.Equal("en", result.Language);
        Assert.Equal("confused", result.Emotion);
        Assert.Equal(0.45, result.EmotionIntensity);
        Assert.Equal("none", result.Gesture);
        Assert.Equal(0.0, result.GestureIntensity);
    }

    [Fact]
    public void Sanitize_TruncatesExcessivelyLongSpeech()
    {
        var raw = new AvatarDecision(
            new string('x', 1_300),
            "en",
            "neutral",
            0.2,
            "none",
            0);

        var result = AvatarDecisionValidator.Sanitize(raw);

        Assert.Equal(1_201, result.Speech.Length);
        Assert.EndsWith("…", result.Speech);
    }
}
