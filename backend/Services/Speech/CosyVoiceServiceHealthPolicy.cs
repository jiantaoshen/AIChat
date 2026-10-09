// This file defines the compatibility contract used before the backend reuses an already-running CosyVoice service.
namespace AiAvatar.Backend.Services.Speech;

public sealed class CosyVoiceHealthSnapshot
{
    public string? Service { get; init; }
    public int? ContractVersion { get; init; }
    public bool Ready { get; init; }
    public string? Model { get; init; }
    public string? ModelPath { get; init; }
    public string? VoiceSource { get; init; }
    public string? ReferenceWavPath { get; init; }
    public string? ReferenceTextPath { get; init; }
}

public sealed record CosyVoiceServiceExpectation(
    string Model,
    string ModelPath,
    string VoiceSource,
    string ReferenceWavPath,
    string? ReferenceTextPath);

public sealed record CosyVoiceHealthValidation(bool CanReuse, string Reason);

public static class CosyVoiceServiceHealthPolicy
{
    public const string ServiceId = "ai-avatar-cosyvoice";
    public const int ContractVersion = 1;

    public static CosyVoiceHealthValidation ValidateIdentity(
        CosyVoiceHealthSnapshot status,
        string expectedModel)
    {
        if (!string.Equals(status.Service, ServiceId, StringComparison.Ordinal))
        {
            return Reject(
                $"service identity mismatch (expected '{ServiceId}', got '{status.Service ?? "<missing>"}')");
        }

        if (status.ContractVersion != ContractVersion)
        {
            return Reject(
                $"health contract mismatch (expected {ContractVersion}, got {status.ContractVersion?.ToString() ?? "<missing>"})");
        }

        if (!string.Equals(status.Model, expectedModel, StringComparison.OrdinalIgnoreCase))
        {
            return Reject(
                $"model mismatch (expected '{expectedModel}', got '{status.Model ?? "<missing>"}')");
        }

        return new CosyVoiceHealthValidation(true, "service identity is compatible");
    }

    public static CosyVoiceHealthValidation ValidateCompatibility(
        CosyVoiceHealthSnapshot status,
        CosyVoiceServiceExpectation expected)
    {
        var identity = ValidateIdentity(status, expected.Model);
        if (!identity.CanReuse)
        {
            return identity;
        }

        if (!PathsEqual(status.ModelPath, expected.ModelPath))
        {
            return Reject(
                $"model path mismatch (expected '{expected.ModelPath}', got '{status.ModelPath ?? "<missing>"}')");
        }

        if (!string.Equals(status.VoiceSource, expected.VoiceSource, StringComparison.Ordinal))
        {
            return Reject(
                $"voice source mismatch (expected '{expected.VoiceSource}', got '{status.VoiceSource ?? "<missing>"}')");
        }

        if (!PathsEqual(status.ReferenceWavPath, expected.ReferenceWavPath))
        {
            return Reject(
                $"reference voice mismatch (expected '{expected.ReferenceWavPath}', got '{status.ReferenceWavPath ?? "<missing>"}')");
        }

        if (!OptionalPathsEqual(status.ReferenceTextPath, expected.ReferenceTextPath))
        {
            return Reject(
                $"reference text mismatch (expected '{expected.ReferenceTextPath ?? "<none>"}', got '{status.ReferenceTextPath ?? "<missing>"}')");
        }

        return new CosyVoiceHealthValidation(true, "service configuration is compatible");
    }

    public static CosyVoiceHealthValidation ValidateForReuse(
        CosyVoiceHealthSnapshot status,
        CosyVoiceServiceExpectation expected)
    {
        var compatibility = ValidateCompatibility(status, expected);
        if (!compatibility.CanReuse)
        {
            return compatibility;
        }

        if (!status.Ready)
        {
            return Reject("the service reports ready=false");
        }

        return new CosyVoiceHealthValidation(true, "compatible and ready");
    }

    private static CosyVoiceHealthValidation Reject(string reason) =>
        new(false, reason);

    private static bool OptionalPathsEqual(string? actual, string? expected)
    {
        if (expected is null)
        {
            return actual is null;
        }

        return PathsEqual(actual, expected);
    }

    private static bool PathsEqual(string? actual, string expected)
    {
        if (string.IsNullOrWhiteSpace(actual) || string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        try
        {
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            return string.Equals(
                Path.GetFullPath(actual),
                Path.GetFullPath(expected),
                comparison);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
