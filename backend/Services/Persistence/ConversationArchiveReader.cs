// This service exposes and deletes persisted SQLite conversations for the browser archive without leaking telemetry or other developer-only data.
using AiAvatar.Backend.Data;
using AiAvatar.Backend.Errors;
using AiAvatar.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace AiAvatar.Backend.Services.Persistence;

public sealed class ConversationArchiveReader(AvatarDbContext db)
{
    public async Task<IReadOnlyList<ConversationArchiveItem>> ListAsync(
        CancellationToken cancellationToken)
    {
        return await db.Conversations
            .AsNoTracking()
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenByDescending(item => item.CreatedAtUtc)
            .Select(item => new ConversationArchiveItem(
                item.Id,
                item.Title,
                item.CreatedAtUtc,
                item.UpdatedAtUtc,
                item.Messages.Count(message =>
                    message.Role == "user" || message.Role == "assistant")))
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var conversation = await db.Conversations
            .SingleOrDefaultAsync(item => item.Id == conversationId, cancellationToken);

        if (conversation is null)
        {
            throw new ResourceNotFoundException(
                $"Conversation '{conversationId}' was not found in the local database.");
        }

        db.Conversations.Remove(conversation);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ConversationSnapshot> GetAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var conversation = await db.Conversations
            .AsNoTracking()
            .Where(item => item.Id == conversationId)
            .Select(item => new
            {
                item.Id,
                item.Title,
                item.CreatedAtUtc,
                item.UpdatedAtUtc,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (conversation is null)
        {
            throw new ResourceNotFoundException(
                $"Conversation '{conversationId}' was not found in the local database.");
        }

        var messages = await db.Messages
            .AsNoTracking()
            .Where(item =>
                item.ConversationId == conversationId &&
                (item.Role == "user" || item.Role == "assistant"))
            .OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .Select(item => new ChatMessage(item.Role, item.Content))
            .ToListAsync(cancellationToken);

        return new ConversationSnapshot(
            conversation.Id,
            conversation.Title,
            conversation.CreatedAtUtc,
            conversation.UpdatedAtUtc,
            messages);
    }
}
