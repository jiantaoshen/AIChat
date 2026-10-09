using AiAvatar.Backend.Models;
using AiAvatar.Backend.Services.Speech;

namespace AiAvatar.Backend.Tests;

public sealed class TtsSpeechPolicyTests
{
    [Theory]
    [InlineData("neutral", 1.0)]
    [InlineData("happy", 1.07)]
    [InlineData("sad", 0.91)]
    [InlineData("angry", 1.04)]
    [InlineData("surprised", 1.07)]
    [InlineData("confused", 0.96)]
    public void CreatePayload_MapsEmotionToDeterministicBoundedSpeed(
        string emotion,
        double expectedSpeed)
    {
        var input = new SpeechSynthesisInput(
            "  hello  ",
            "en",
            emotion,
            1.0);

        var payload = TtsSpeechPolicy.CreatePayload(input);

        Assert.Equal("hello", payload.Text);
        Assert.InRange(Math.Abs(payload.Speed - expectedSpeed), 0, 1e-10);
        Assert.InRange(payload.Speed, 0.88, 1.12);
        Assert.False(payload.Instruction.Contains("<|endofprompt|>", StringComparison.Ordinal));
    }

    [Fact]
    public void CreatePayload_UnknownEmotionFallsBackToNeutral()
    {
        var input = new SpeechSynthesisInput(
            "hello",
            "en",
            "ecstatic",
            double.PositiveInfinity);

        var payload = TtsSpeechPolicy.CreatePayload(input);

        Assert.Equal(1.0, payload.Speed);
        Assert.True(payload.Instruction.Contains("calm, natural, clear", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("zh")]
    [InlineData("en")]
    [InlineData("EN-us")]
    [InlineData("ja_JP")]
    [InlineData("ru")]
    public void IsSupportedLanguage_AcceptsBackendOwnedSupportedLanguages(string value)
    {
        Assert.True(TtsSpeechPolicy.IsSupportedLanguage(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("sv")]
    [InlineData("sv-SE")]
    [InlineData("und")]
    public void IsSupportedLanguage_RejectsUnsupportedLanguages(string value)
    {
        Assert.False(TtsSpeechPolicy.IsSupportedLanguage(value));
    }

    [Theory]
    [InlineData("neutral")]
    [InlineData(" HAPPY ")]
    [InlineData("confused")]
    public void IsSupportedEmotion_AcceptsKnownValues(string value)
    {
        Assert.True(TtsSpeechPolicy.IsSupportedEmotion(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ecstatic")]
    public void IsSupportedEmotion_RejectsUnknownValues(string value)
    {
        Assert.False(TtsSpeechPolicy.IsSupportedEmotion(value));
    }
}
