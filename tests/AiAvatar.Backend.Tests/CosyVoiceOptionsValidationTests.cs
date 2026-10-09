using AiAvatar.Backend.Options;

namespace AiAvatar.Backend.Tests;

public sealed class CosyVoiceOptionsValidationTests
{
    [Fact]
    public void DefaultConfiguration_IsStructurallyValid()
    {
        var options = new CosyVoiceOptions();

        Assert.True(CosyVoiceOptionsValidation.HasValidEndpoint(options));
        Assert.True(CosyVoiceOptionsValidation.HasValidLimits(options));
        Assert.True(CosyVoiceOptionsValidation.HasRequiredRuntimePaths(options));
        Assert.True(CosyVoiceOptionsValidation.HasRequiredAutoStartPaths(options));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65_536)]
    public void EnabledConfiguration_RejectsInvalidPort(int port)
    {
        var options = new CosyVoiceOptions { Port = port };

        Assert.False(CosyVoiceOptionsValidation.HasValidEndpoint(options));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("http://127.0.0.1")]
    public void EnabledConfiguration_RejectsInvalidHost(string host)
    {
        var options = new CosyVoiceOptions { Host = host };

        Assert.False(CosyVoiceOptionsValidation.HasValidEndpoint(options));
    }

    [Fact]
    public void EnabledConfiguration_RejectsNonPositiveLimits()
    {
        Assert.False(CosyVoiceOptionsValidation.HasValidLimits(
            new CosyVoiceOptions { StartupTimeoutSeconds = 0 }));
        Assert.False(CosyVoiceOptionsValidation.HasValidLimits(
            new CosyVoiceOptions { SynthesisTimeoutSeconds = 0 }));
        Assert.False(CosyVoiceOptionsValidation.HasValidLimits(
            new CosyVoiceOptions { MaxTextCharacters = 0 }));
    }

    [Theory]
    [InlineData("repo")]
    [InlineData("model")]
    [InlineData("reference-audio")]
    [InlineData("reference-text")]
    public void EnabledConfiguration_RejectsMissingRuntimePath(string missing)
    {
        var options = new CosyVoiceOptions();

        switch (missing)
        {
            case "repo":
                options.CosyVoiceRepo = "";
                break;
            case "model":
                options.ModelPath = "";
                break;
            case "reference-audio":
                options.ReferenceAudioPath = "";
                break;
            case "reference-text":
                options.ReferenceTextPath = "";
                break;
        }

        Assert.False(CosyVoiceOptionsValidation.HasRequiredRuntimePaths(options));
    }

    [Fact]
    public void AutoStart_RequiresPythonAndServiceScript()
    {
        Assert.False(CosyVoiceOptionsValidation.HasRequiredAutoStartPaths(
            new CosyVoiceOptions { PythonExecutable = "" }));
        Assert.False(CosyVoiceOptionsValidation.HasRequiredAutoStartPaths(
            new CosyVoiceOptions { ServiceScript = "" }));
    }

    [Fact]
    public void ManualStart_DoesNotRequirePythonOrServiceScript()
    {
        var options = new CosyVoiceOptions
        {
            AutoStart = false,
            PythonExecutable = "",
            ServiceScript = "",
        };

        Assert.True(CosyVoiceOptionsValidation.HasRequiredAutoStartPaths(options));
    }

    [Fact]
    public void DisabledCosyVoice_DoesNotBlockBackendStartupForUnusedSpeechSettings()
    {
        var options = new CosyVoiceOptions
        {
            Enabled = false,
            Host = "",
            Port = -1,
            StartupTimeoutSeconds = 0,
            SynthesisTimeoutSeconds = 0,
            MaxTextCharacters = 0,
            PythonExecutable = "",
            ServiceScript = "",
            CosyVoiceRepo = "",
            ModelPath = "",
            ReferenceAudioPath = "",
            ReferenceTextPath = "",
        };

        Assert.True(CosyVoiceOptionsValidation.HasValidEndpoint(options));
        Assert.True(CosyVoiceOptionsValidation.HasValidLimits(options));
        Assert.True(CosyVoiceOptionsValidation.HasRequiredRuntimePaths(options));
        Assert.True(CosyVoiceOptionsValidation.HasRequiredAutoStartPaths(options));
    }
}
