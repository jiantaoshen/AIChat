// This endpoint group accepts only a persisted assistant message reference. Speech content and emotion are loaded by the backend from SQLite.
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
            if (!cosyVoiceOptions.Value.Enabled)
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

            var result = await speechSynthesisService.SynthesizeAsync(
                request.MessageId,
                cancellationToken);

            return Results.File(result.Audio, "audio/wav");
        });

        return endpoints;
    }
}
