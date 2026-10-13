// This endpoint group validates one idempotent chat turn and delegates backend-owned history plus atomic persistence to ChatTurnService.
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

        endpoints.MapGet("/api/conversations", async (
            ConversationArchiveReader archiveReader,
            CancellationToken cancellationToken) =>
        {
            var conversations = await archiveReader.ListAsync(cancellationToken);
            return Results.Ok(conversations);
        });

        endpoints.MapGet("/api/conversations/{conversationId:guid}", async (
            Guid conversationId,
            ConversationArchiveReader archiveReader,
            CancellationToken cancellationToken) =>
        {
            var conversation = await archiveReader.GetAsync(
                conversationId,
                cancellationToken);
            return Results.Ok(conversation);
        });

        return endpoints;
    }
}
