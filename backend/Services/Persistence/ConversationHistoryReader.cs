// This reader owns the turn-aware context-window policy used for model inference.
using AiAvatar.Backend.Data;
using AiAvatar.Backend.Errors;
using AiAvatar.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace AiAvatar.Backend.Services.Persistence;

public sealed class ConversationHistoryReader(AvatarDbContext db) : IConversationHistoryReader
{
    private const int MaxCompletedContextTurns = 5;

    public async Task<IReadOnlyList<ChatMessage>> BuildModelContextAsync(
        Guid? conversationId,
        string currentUserMessage,
        CancellationToken cancellationToken)
    {
        var current = new ChatMessage("user", currentUserMessage.Trim());
        if (conversationId is null)
        {
            return [current];
        }

        if (!await db.Conversations
                .AsNoTracking()
                .AnyAsync(item => item.Id == conversationId.Value, cancellationToken))
        {
            throw new ResourceNotFoundException(
                $"Conversation '{conversationId}' was not found in the local database.");
        }

        // Context selection is turn-aware. A persisted history item is eligible only when
        // a user message and an assistant message share the same non-null TurnId. This
        // prevents an assistant message from crossing the truncation boundary without its
        // user prompt. Rows from the pre-TurnId schema are intentionally excluded because
        // their turn boundary cannot be proven reliably.
        var recentTurns = await (
                from user in db.Messages.AsNoTracking()
                join assistant in db.Messages.AsNoTracking()
                    on new { user.ConversationId, user.TurnId }
                    equals new { assistant.ConversationId, assistant.TurnId }
                where
                    user.ConversationId == conversationId.Value &&
                    user.TurnId != null &&
                    user.Role == "user" &&
                    assistant.Role == "assistant"
                orderby assistant.CreatedAtUtc descending, assistant.Id descending
                select new
                {
                    UserContent = user.Content,
                    AssistantContent = assistant.Content,
                })
            .Take(MaxCompletedContextTurns)
            .ToListAsync(cancellationToken);

        recentTurns.Reverse();
        var history = new List<ChatMessage>(recentTurns.Count * 2 + 1);
        foreach (var turn in recentTurns)
        {
            history.Add(new ChatMessage("user", turn.UserContent));
            history.Add(new ChatMessage("assistant", turn.AssistantContent));
        }

        history.Add(current);
        return history;
    }
}
