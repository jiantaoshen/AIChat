// This endpoint group validates one idempotent chat turn and delegates backend-owned history plus atomic persistence to ChatTurnService.
using AiAvatar.Backend.Models;
using AiAvatar.Backend.Services;
using AiAvatar.Backend.Services.Persistence;

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

            try
            {
                var response = await chatTurnService.ExecuteAsync(
                    request,
                    cancellationToken);
                return Results.Ok(response);
            }
            catch (ChatTurnConflictException exception)
            {
                return Results.Conflict(new { error = exception.Message });
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
                    title: "Local Ollama request failed");
            }
            catch (TaskCanceledException exception)
            {
                return Results.Problem(
                    detail: $"Local model request timed out: {exception.Message}",
                    statusCode: StatusCodes.Status504GatewayTimeout,
                    title: "Qwen inference timed out");
            }
            catch (Exception exception)
            {
                return Results.Problem(
                    detail: exception.Message,
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Unexpected backend error");
            }
        });

        return endpoints;
    }
}
