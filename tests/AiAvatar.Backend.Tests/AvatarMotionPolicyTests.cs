using AiAvatar.Backend.Models;
using AiAvatar.Backend.Services;

namespace AiAvatar.Backend.Tests;

public sealed class AvatarMotionPolicyTests
{
    [Fact]
    public void Neutral_RemovesJump()
    {
        var result = AvatarMotionPolicy.Apply(Decision("neutral", 0.5, "jump", 0.9));

        Assert.Equal("none", result.Gesture);
        Assert.Equal(0.0, result.GestureIntensity);
    }

    [Fact]
    public void Happy_HighIntensityAddsRestrainedJump()
    {
        var result = AvatarMotionPolicy.Apply(Decision("happy", 0.9, "none", 0));

        Assert.Equal("jump", result.Gesture);
        Assert.Equal(0.38, result.GestureIntensity);
    }

    [Fact]
    public void Angry_ConvertsJumpToBoundedShake()
    {
        var result = AvatarMotionPolicy.Apply(Decision("angry", 0.9, "jump", 0.95));

        Assert.Equal("shake", result.Gesture);
        Assert.Equal(0.62, result.GestureIntensity);
    }

    [Fact]
    public void Sad_RemovesAggressiveGesture()
    {
        var result = AvatarMotionPolicy.Apply(Decision("sad", 0.8, "shake", 0.7));

        Assert.Equal("none", result.Gesture);
        Assert.Equal(0.0, result.GestureIntensity);
    }

    [Fact]
    public void Confused_ConvertsJumpToSmallShake()
    {
        var result = AvatarMotionPolicy.Apply(Decision("confused", 0.8, "jump", 0.9));

        Assert.Equal("shake", result.Gesture);
        Assert.Equal(0.2, result.GestureIntensity);
    }

    private static AvatarDecision Decision(
        string emotion,
        double emotionIntensity,
        string gesture,
        double gestureIntensity) =>
        new("speech", "en", emotion, emotionIntensity, gesture, gestureIntensity);
}
