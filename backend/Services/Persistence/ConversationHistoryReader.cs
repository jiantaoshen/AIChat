// This reader owns server-authoritative, turn-aware history selection for model inference.
using AiAvatar.Backend.Data;
using AiAvatar.Backend.Errors;
using AiAvatar.Backend.Models;
using AiAvatar.Backend.Services.Ollama;
using Microsoft.EntityFrameworkCore;

namespace AiAvatar.Backend.Services.Persistence;

public sealed class ConversationHistoryReader(
    AvatarDbContext db,
    ConversationContextPolicy contextPolicy) : IConversationHistoryReader
{
    public async Task<IReadOnlyList<ChatMessage>> BuildModelContextAsync(
        Guid? conversationId,
        string currentUserMessage,
        CancellationToken cancellationToken)
    {
        var current = new ChatMessage("user", currentUserMessage.Trim());
        if (!contextPolicy.CanFitCurrentUserMessage(current.Content))
        {
            throw new ArgumentException(
                "The current user message cannot fit within the configured model context budget.",
                nameof(currentUserMessage));
        }

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

        // A history item is eligible only as a proven complete turn: one user message and
        // one assistant message sharing the same non-null TurnId. Rows from the pre-TurnId
        // schema remain persisted but are intentionally excluded because their boundary
        // cannot be reconstructed safely.
        //
        // The query itself is also bounded. The maximum candidate count is derived from
        // the remaining token budget and the minimum framing cost of a complete turn; it
        // is not a fixed business limit such as "5 turns".
        var maxCandidateTurns = contextPolicy.GetMaxCandidateCompletedTurns(current.Content);
        if (maxCandidateTurns == 0)
        {
            return [current];
        }

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
            .Take(maxCandidateTurns)
            .ToListAsync(cancellationToken);

        var remainingHistoryBudget = contextPolicy.GetHistoryBudgetTokens(current.Content);
        var selectedNewestFirst = new List<(string UserContent, string AssistantContent)>();

        foreach (var turn in recentTurns)
        {
            var turnCost = contextPolicy.EstimateCompleteTurnTokens(
                turn.UserContent,
                turn.AssistantContent);

            // Keep recent history contiguous. If the next most recent complete turn cannot
            // fit, do not skip it in order to cherry-pick older/smaller turns.
            if (turnCost > remainingHistoryBudget)
            {
                break;
            }

            selectedNewestFirst.Add((turn.UserContent, turn.AssistantContent));
            remainingHistoryBudget -= turnCost;
        }

        selectedNewestFirst.Reverse();
        var context = new List<ChatMessage>(selectedNewestFirst.Count * 2 + 1);
        foreach (var turn in selectedNewestFirst)
        {
            context.Add(new ChatMessage("user", turn.UserContent));
            context.Add(new ChatMessage("assistant", turn.AssistantContent));
        }

        context.Add(current);
        return context;
    }
}
