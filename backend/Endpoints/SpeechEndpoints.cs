// This endpoint group validates speech HTTP input and delegates persisted-message synthesis plus telemetry to SpeechSynthesisService.
using AiAvatar.Backend.Models;
using AiAvatar.Backend.Options;
using AiAvatar.Backend.Services.Speech;
using Microsoft.Extensions.Options;

namespace AiAvatar.Backend.Endpoints;

public static class SpeechEndpoints
{
    public static IEndpointRouteBuilder MapSpeechEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/speech", async (
            SpeechSynthesisRequest request,
            SpeechSynthesisService speechSynthesisService,
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

            var result = await speechSynthesisService.SynthesizeAsync(
                request,
                cancellationToken);

            return Results.File(result.Audio, "audio/wav");
        });

        return endpoints;
    }
}
