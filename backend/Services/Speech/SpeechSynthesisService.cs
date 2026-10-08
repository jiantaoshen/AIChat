// This service owns one speech operation: validate the persisted assistant message, call CosyVoice, then persist TTS telemetry.
using AiAvatar.Backend.Errors;
using AiAvatar.Backend.Models;
using AiAvatar.Backend.Services.Persistence;

namespace AiAvatar.Backend.Services.Speech;

public sealed class SpeechSynthesisService(
    CosyVoiceClient cosyVoice,
    IConversationStore conversationStore)
{
    public async Task<SpeechSynthesisResult> SynthesizeAsync(
        SpeechSynthesisRequest request,
        CancellationToken cancellationToken)
    {
        if (!await conversationStore.AssistantMessageExistsAsync(
                request.MessageId,
                cancellationToken))
        {
            throw new ResourceNotFoundException(
                $"Assistant message '{request.MessageId}' was not found in the local database.");
        }

        var result = await cosyVoice.SynthesizeAsync(request, cancellationToken);

        await conversationStore.SaveTtsTelemetryAsync(
            request.MessageId,
            result,
            cancellationToken);

        return result;
    }
}
