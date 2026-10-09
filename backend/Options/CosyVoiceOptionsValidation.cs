// This file defines structural startup validation for CosyVoice configuration.
// It validates configuration shape only; runtime installation/readiness checks stay in the hosted service and health handshake.
namespace AiAvatar.Backend.Options;

public static class CosyVoiceOptionsValidation
{
    public static bool HasValidEndpoint(CosyVoiceOptions options)
    {
        if (!options.Enabled)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(options.Host) ||
            options.Host.Contains("://", StringComparison.Ordinal) ||
            options.Port is < 1 or > 65_535)
        {
            return false;
        }

        return Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) &&
               uri.Scheme == Uri.UriSchemeHttp &&
               !string.IsNullOrWhiteSpace(uri.Host);
    }

    public static bool HasValidLimits(CosyVoiceOptions options)
    {
        return !options.Enabled ||
               (options.StartupTimeoutSeconds > 0 &&
                options.SynthesisTimeoutSeconds > 0 &&
                options.MaxTextCharacters > 0);
    }

    public static bool HasRequiredRuntimePaths(CosyVoiceOptions options)
    {
        return !options.Enabled ||
               (!string.IsNullOrWhiteSpace(options.CosyVoiceRepo) &&
                !string.IsNullOrWhiteSpace(options.ModelPath) &&
                !string.IsNullOrWhiteSpace(options.ReferenceAudioPath) &&
                !string.IsNullOrWhiteSpace(options.ReferenceTextPath));
    }

    public static bool HasRequiredAutoStartPaths(CosyVoiceOptions options)
    {
        return !options.Enabled ||
               !options.AutoStart ||
               (!string.IsNullOrWhiteSpace(options.PythonExecutable) &&
                !string.IsNullOrWhiteSpace(options.ServiceScript));
    }
}
