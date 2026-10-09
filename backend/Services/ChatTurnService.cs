// This service defines the chat-turn boundary: per-conversation serialization -> server-owned history -> Ollama -> one atomic, idempotent persistence commit.
using AiAvatar.Backend.Models;
using AiAvatar.Backend.Services.Concurrency;
using AiAvatar.Backend.Services.Ollama;
using AiAvatar.Backend.Services.Persistence;

namespace AiAvatar.Backend.Services;

public sealed class ChatTurnService(
    IChatDecisionGenerator decisionGenerator,
    IConversationStore conversationStore,
    ConversationTurnGate turnGate)
{
    public async Task<AvatarChatResponse> ExecuteAsync(
        ChatRequest request,
        CancellationToken cancellationToken)
    {
        // The lease intentionally spans idempotency lookup, context read, model inference,
        // and commit. A later turn for the same conversation must not read history until
        // the earlier turn has either committed or failed and released the lease.
        using var lease = await turnGate.AcquireAsync(
            request.ConversationId,
            request.TurnId,
            cancellationToken);

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

        var ollamaResult = await decisionGenerator.CreateDecisionAsync(
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
