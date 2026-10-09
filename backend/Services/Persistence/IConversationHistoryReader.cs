// This interface exposes the server-owned, turn-aware model context for one conversation.
using AiAvatar.Backend.Models;

namespace AiAvatar.Backend.Services.Persistence;

public interface IConversationHistoryReader
{
    Task<IReadOnlyList<ChatMessage>> BuildModelContextAsync(
        Guid? conversationId,
        string currentUserMessage,
        CancellationToken cancellationToken);
}
