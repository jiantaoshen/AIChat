using AiAvatar.Backend.Services.Speech;

namespace AiAvatar.Backend.Tests;

public sealed class CosyVoiceServiceHealthPolicyTests
{
    [Fact]
    public void ValidateForReuse_AcceptsReadyMatchingServiceModelAndVoice()
    {
        var modelPath = Path.GetFullPath(Path.Combine("models", "Fun-CosyVoice3-0.5B-2512"));
        var referencePath = Path.GetFullPath(Path.Combine("voice", "reference.wav"));
        var expected = new CosyVoiceServiceExpectation(
            "Fun-CosyVoice3-0.5B-2512",
            modelPath,
            "custom-reference",
            referencePath,
            Path.GetFullPath(Path.Combine("voice", "reference.txt")));
        var status = new CosyVoiceHealthSnapshot
        {
            Service = CosyVoiceServiceHealthPolicy.ServiceId,
            ContractVersion = CosyVoiceServiceHealthPolicy.ContractVersion,
            Ready = true,
            Model = expected.Model,
            ModelPath = modelPath,
            VoiceSource = expected.VoiceSource,
            ReferenceWavPath = referencePath,
            ReferenceTextPath = expected.ReferenceTextPath,
        };

        var result = CosyVoiceServiceHealthPolicy.ValidateForReuse(status, expected);

        Assert.True(result.CanReuse);
    }

    [Fact]
    public void ValidateForReuse_RejectsUnidentifiedListener()
    {
        var expected = CreateExpectation();
        var status = CreateMatchingStatus(expected);
        status = new CosyVoiceHealthSnapshot
        {
            Service = "some-other-service",
            ContractVersion = status.ContractVersion,
            Ready = status.Ready,
            Model = status.Model,
            ModelPath = status.ModelPath,
            VoiceSource = status.VoiceSource,
            ReferenceWavPath = status.ReferenceWavPath,
            ReferenceTextPath = status.ReferenceTextPath,
        };

        var result = CosyVoiceServiceHealthPolicy.ValidateForReuse(status, expected);

        Assert.False(result.CanReuse);
        Assert.Contains("service identity mismatch", result.Reason);
    }

    [Fact]
    public void ValidateForReuse_RejectsStaleModelConfiguration()
    {
        var expected = CreateExpectation();
        var status = CreateMatchingStatus(expected);
        status = new CosyVoiceHealthSnapshot
        {
            Service = status.Service,
            ContractVersion = status.ContractVersion,
            Ready = status.Ready,
            Model = "Old-CosyVoice-Model",
            ModelPath = Path.GetFullPath(Path.Combine("models", "Old-CosyVoice-Model")),
            VoiceSource = status.VoiceSource,
            ReferenceWavPath = status.ReferenceWavPath,
            ReferenceTextPath = status.ReferenceTextPath,
        };

        var result = CosyVoiceServiceHealthPolicy.ValidateForReuse(status, expected);

        Assert.False(result.CanReuse);
        Assert.Contains("model mismatch", result.Reason);
    }

    [Fact]
    public void ValidateForReuse_RejectsStaleVoiceConfiguration()
    {
        var expected = CreateExpectation();
        var status = CreateMatchingStatus(expected);
        status = new CosyVoiceHealthSnapshot
        {
            Service = status.Service,
            ContractVersion = status.ContractVersion,
            Ready = status.Ready,
            Model = status.Model,
            ModelPath = status.ModelPath,
            VoiceSource = status.VoiceSource,
            ReferenceWavPath = Path.GetFullPath(Path.Combine("voice", "old-reference.wav")),
            ReferenceTextPath = status.ReferenceTextPath,
        };

        var result = CosyVoiceServiceHealthPolicy.ValidateForReuse(status, expected);

        Assert.False(result.CanReuse);
        Assert.Contains("reference voice mismatch", result.Reason);
    }

    [Fact]
    public void ValidateForReuse_RejectsServiceThatIsNotReady()
    {
        var expected = CreateExpectation();
        var matching = CreateMatchingStatus(expected);
        var status = new CosyVoiceHealthSnapshot
        {
            Service = matching.Service,
            ContractVersion = matching.ContractVersion,
            Ready = false,
            Model = matching.Model,
            ModelPath = matching.ModelPath,
            VoiceSource = matching.VoiceSource,
            ReferenceWavPath = matching.ReferenceWavPath,
            ReferenceTextPath = matching.ReferenceTextPath,
        };

        var result = CosyVoiceServiceHealthPolicy.ValidateForReuse(status, expected);

        Assert.False(result.CanReuse);
        Assert.Contains("ready=false", result.Reason);
    }

    private static CosyVoiceServiceExpectation CreateExpectation() =>
        new(
            "Fun-CosyVoice3-0.5B-2512",
            Path.GetFullPath(Path.Combine("models", "Fun-CosyVoice3-0.5B-2512")),
            "custom-reference",
            Path.GetFullPath(Path.Combine("voice", "reference.wav")),
            Path.GetFullPath(Path.Combine("voice", "reference.txt")));

    private static CosyVoiceHealthSnapshot CreateMatchingStatus(
        CosyVoiceServiceExpectation expected) =>
        new()
        {
            Service = CosyVoiceServiceHealthPolicy.ServiceId,
            ContractVersion = CosyVoiceServiceHealthPolicy.ContractVersion,
            Ready = true,
            Model = expected.Model,
            ModelPath = expected.ModelPath,
            VoiceSource = expected.VoiceSource,
            ReferenceWavPath = expected.ReferenceWavPath,
            ReferenceTextPath = expected.ReferenceTextPath,
        };
}
