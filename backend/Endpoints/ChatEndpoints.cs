// This endpoint group validates one idempotent chat turn and delegates backend-owned history plus atomic persistence to ChatTurnService.
using AiAvatar.Backend.Models;
using AiAvatar.Backend.Services;

namespace AiAvatar.Backend.Endpoints;

public static class ChatEndpoints
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/chat", async (
            ChatRequest request,
            ChatTurnService chatTurnService,
            CancellationToken cancellationToken) =>
        {
            var validationError = ChatRequestValidator.Validate(request);
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
