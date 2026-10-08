// This service defines the chat-turn boundary: server-owned history -> Ollama -> one atomic, idempotent persistence commit.
using AiAvatar.Backend.Models;
using AiAvatar.Backend.Services.Ollama;
using AiAvatar.Backend.Services.Persistence;

namespace AiAvatar.Backend.Services;

public sealed class ChatTurnService(
    OllamaClient ollama,
    IConversationStore conversationStore)
{
    public async Task<AvatarChatResponse> ExecuteAsync(
        ChatRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await conversationStore.TryGetCompletedTurnAsync(
            request.TurnId,
            request.ConversationId,
            request.Message,
            cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var context = await conversationStore.BuildModelContextAsync(
            request.ConversationId,
            request.Message,
            cancellationToken);

        var ollamaResult = await ollama.CreateDecisionAsync(
            context,
            cancellationToken);

        return await conversationStore.CommitTurnAsync(
            request.ConversationId,
            request.TurnId,
            request.Message,
            ollamaResult,
            cancellationToken);
    }
}
