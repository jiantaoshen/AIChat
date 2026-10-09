// This service owns one speech operation: load authoritative assistant semantics from SQLite, validate them, call CosyVoice, then persist TTS telemetry.
using AiAvatar.Backend.Errors;
using AiAvatar.Backend.Models;
using AiAvatar.Backend.Options;
using AiAvatar.Backend.Services.Persistence;
using Microsoft.Extensions.Options;

namespace AiAvatar.Backend.Services.Speech;

public sealed class SpeechSynthesisService(
    CosyVoiceClient cosyVoice,
    ISpeechRepository speechRepository,
    IOptions<CosyVoiceOptions> cosyVoiceOptions)
{
    private readonly CosyVoiceOptions _options = cosyVoiceOptions.Value;

    public async Task<SpeechSynthesisResult> SynthesizeAsync(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        var source = await speechRepository.GetAssistantSpeechSourceAsync(
            messageId,
            cancellationToken);

        if (source is null)
        {
            throw new ResourceNotFoundException(
                $"Assistant message '{messageId}' was not found in the local database.");
        }

        var input = BuildValidatedInput(source);
        var result = await cosyVoice.SynthesizeAsync(input, cancellationToken);

        await speechRepository.SaveTtsTelemetryAsync(
            messageId,
            result,
            cancellationToken);

        return result;
    }

    private SpeechSynthesisInput BuildValidatedInput(AssistantSpeechSource source)
    {
        if (string.IsNullOrWhiteSpace(source.Text))
        {
            throw new SpeechSynthesisRejectedException(
                "The persisted assistant message has no speech text to synthesize.");
        }

        if (source.Text.Length > _options.MaxTextCharacters)
        {
            throw new SpeechSynthesisRejectedException(
                $"The persisted assistant message exceeds the configured {_options.MaxTextCharacters} character TTS limit.");
        }

        var language = TtsSpeechPolicy.NormalizeLanguageCode(source.Language);
        if (!TtsSpeechPolicy.IsSupportedLanguage(language))
        {
            throw new SpeechSynthesisRejectedException(
                "The persisted assistant message language is not supported by the backend TTS policy.");
        }

        if (!TtsSpeechPolicy.IsSupportedEmotion(source.Emotion))
        {
            throw new SpeechSynthesisRejectedException(
                "The persisted assistant message contains an unsupported speech emotion.");
        }

        if (source.EmotionIntensity is null ||
            double.IsNaN(source.EmotionIntensity.Value) ||
            double.IsInfinity(source.EmotionIntensity.Value) ||
            source.EmotionIntensity.Value < 0 ||
            source.EmotionIntensity.Value > 1)
        {
            throw new SpeechSynthesisRejectedException(
                "The persisted assistant message contains an invalid speech emotion intensity.");
        }

        return new SpeechSynthesisInput(
            source.Text,
            language,
            source.Emotion!,
            source.EmotionIntensity.Value);
    }
}
