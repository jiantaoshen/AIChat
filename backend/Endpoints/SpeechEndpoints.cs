// This endpoint group validates persisted assistant speech requests, calls local CosyVoice, stores TTS telemetry, and returns WAV audio to the browser.
using AiAvatar.Backend.Models;
using AiAvatar.Backend.Options;
using AiAvatar.Backend.Services.Persistence;
using AiAvatar.Backend.Services.Speech;
using Microsoft.Extensions.Options;

namespace AiAvatar.Backend.Endpoints;

public static class SpeechEndpoints
{
    public static IEndpointRouteBuilder MapSpeechEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/speech", async (
            SpeechSynthesisRequest request,
            CosyVoiceClient cosyVoice,
            IConversationStore conversationStore,
            IOptions<CosyVoiceOptions> cosyVoiceOptions,
            CancellationToken cancellationToken) =>
        {
            var options = cosyVoiceOptions.Value;

            if (!options.Enabled)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Local TTS is disabled",
                    detail: "Enable the CosyVoice section in appsettings.json before using avatar speech.");
            }

            if (request.MessageId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "A persisted assistant messageId is required for speech." });
            }

            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return Results.BadRequest(new { error = "Speech text cannot be empty." });
            }

            if (request.Text.Length > options.MaxTextCharacters)
            {
                return Results.BadRequest(new
                {
                    error = $"Speech text exceeds the configured {options.MaxTextCharacters} character limit.",
                });
            }

            if (!TtsSpeechPolicy.IsSupportedEmotion(request.Emotion))
            {
                return Results.BadRequest(new { error = $"Unsupported emotion: {request.Emotion}" });
            }

            if (!await conversationStore.AssistantMessageExistsAsync(
                    request.MessageId,
                    cancellationToken))
            {
                return Results.NotFound(new
                {
                    error = $"Assistant message '{request.MessageId}' was not found in the local database.",
                });
            }

            try
            {
                var result = await cosyVoice.SynthesizeAsync(request, cancellationToken);

                await conversationStore.SaveTtsTelemetryAsync(
                    request.MessageId,
                    result,
                    cancellationToken);

                return Results.File(result.Audio, "audio/wav");
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (HttpRequestException exception)
            {
                return Results.Problem(
                    detail: exception.Message,
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Local CosyVoice request failed");
            }
            catch (TaskCanceledException exception)
            {
                return Results.Problem(
                    detail: $"Local text-to-speech timed out: {exception.Message}",
                    statusCode: StatusCodes.Status504GatewayTimeout,
                    title: "CosyVoice synthesis timed out");
            }
            catch (Exception exception)
            {
                return Results.Problem(
                    detail: exception.Message,
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Unexpected local TTS error");
            }
        });

        return endpoints;
    }
}
