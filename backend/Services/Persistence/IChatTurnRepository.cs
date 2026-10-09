// This interface owns completed-turn idempotency lookup and atomic chat-turn persistence.
using AiAvatar.Backend.Models;

namespace AiAvatar.Backend.Services.Persistence;

public interface IChatTurnRepository
{
    Task<AvatarChatResponse?> TryGetCompletedTurnAsync(
        Guid turnId,
        Guid? requestedConversationId,
        string userMessage,
        CancellationToken cancellationToken);

    Task<AvatarChatResponse> CommitTurnAsync(
        Guid? conversationId,
        Guid turnId,
        string userMessage,
        OllamaDecisionResult ollamaResult,
        CancellationToken cancellationToken);
}
