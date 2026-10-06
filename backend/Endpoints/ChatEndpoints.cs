// This endpoint group validates chat input, persists the current turn, asks Ollama for an avatar decision, stores telemetry, and returns the browser response.
using AiAvatar.Backend.Models;
using AiAvatar.Backend.Services;
using AiAvatar.Backend.Services.Ollama;
using AiAvatar.Backend.Services.Persistence;

namespace AiAvatar.Backend.Endpoints;

public static class ChatEndpoints
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/chat", async (
            ChatRequest request,
            OllamaClient ollama,
            IConversationStore conversationStore,
            CancellationToken cancellationToken) =>
        {
            var validationError = ChatRequestValidator.Validate(request);
            if (validationError is not null)
            {
                return Results.BadRequest(new { error = validationError });
            }

            var latestUserMessage = request.Messages[^1];

            try
            {
                var conversation = await conversationStore.GetOrCreateConversationAsync(
                    request.ConversationId,
                    latestUserMessage.Content,
                    cancellationToken);

                await conversationStore.AddUserMessageAsync(
                    conversation.Id,
                    latestUserMessage.Content,
                    cancellationToken);

                var ollamaResult = await ollama.CreateDecisionAsync(
                    request.Messages,
                    cancellationToken);

                var assistantMessage = await conversationStore.AddAssistantMessageAsync(
                    conversation.Id,
                    ollamaResult.Decision,
                    cancellationToken);

                await conversationStore.SaveLlmTelemetryAsync(
                    assistantMessage.Id,
                    ollamaResult.Telemetry,
                    cancellationToken);

                return Results.Ok(new AvatarChatResponse(
                    conversation.Id,
                    assistantMessage.Id,
                    ollamaResult.Decision,
                    ollamaResult.Telemetry));
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
