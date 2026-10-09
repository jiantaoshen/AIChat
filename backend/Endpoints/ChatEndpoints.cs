// This endpoint group validates one idempotent chat turn and delegates backend-owned history plus atomic persistence to ChatTurnService.
using AiAvatar.Backend.Models;
using AiAvatar.Backend.Services;
using AiAvatar.Backend.Services.Ollama;

namespace AiAvatar.Backend.Endpoints;

public static class ChatEndpoints
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/chat", async (
            ChatRequest request,
            ChatTurnService chatTurnService,
            ConversationContextPolicy contextPolicy,
            CancellationToken cancellationToken) =>
        {
            var validationError = ChatRequestValidator.Validate(request, contextPolicy);
            if (validationError is not null)
            {
                return Results.BadRequest(new { error = validationError });
            }

            var response = await chatTurnService.ExecuteAsync(
                request,
                cancellationToken);

            return Results.Ok(response);
        });

        return endpoints;
    }
}
