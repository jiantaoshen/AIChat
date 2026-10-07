// This endpoint group reports whether the backend, SQLite integration, Ollama model, and CosyVoice service are ready for local use.
using AiAvatar.Backend.Services;
using AiAvatar.Backend.Services.Ollama;
using AiAvatar.Backend.Services.Speech;

namespace AiAvatar.Backend.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health", async (
            OllamaClient ollama,
            CosyVoiceClient cosyVoice,
            CancellationToken cancellationToken) =>
        {
            var ollamaStatusTask = ollama.GetStatusAsync(cancellationToken);
            var cosyVoiceReadyTask = cosyVoice.IsReadyAsync(cancellationToken);

            await Task.WhenAll(ollamaStatusTask, cosyVoiceReadyTask);

            var ollamaStatus = await ollamaStatusTask;
            var cosyVoiceReady = await cosyVoiceReadyTask;

            return Results.Ok(new
            {
                backend = "ok",
                database = "sqlite",
                ollamaReachable = ollamaStatus.Reachable,
                modelInstalled = ollamaStatus.ModelInstalled,
                ollamaMessage = ollamaStatus.Message,
                cosyVoiceReady,
            });
        });

        return endpoints;
    }
}
