// This service persists local conversations, messages, Qwen telemetry, and CosyVoice telemetry through EF Core SQLite.
using AiAvatar.Backend.Data;
using AiAvatar.Backend.Data.Entities;
using AiAvatar.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace AiAvatar.Backend.Services.Persistence;

public sealed class ConversationStore(AvatarDbContext db) : IConversationStore
{
    private const int MaxConversationTitleLength = 80;

    public async Task<ConversationEntity> GetOrCreateConversationAsync(
        Guid? conversationId,
        string firstUserMessage,
        CancellationToken cancellationToken)
    {
        if (conversationId is Guid existingId)
        {
            var existing = await db.Conversations.FindAsync(
                [existingId],
                cancellationToken);

            if (existing is null)
            {
                throw new KeyNotFoundException(
                    $"Conversation '{existingId}' was not found in the local database.");
            }

            return existing;
        }

        var now = DateTime.UtcNow;
        var conversation = new ConversationEntity
        {
            Title = BuildConversationTitle(firstUserMessage),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        db.Conversations.Add(conversation);
        await db.SaveChangesAsync(cancellationToken);
        return conversation;
    }

    public async Task<IReadOnlyList<ChatMessage>> GetRecentMessagesAsync(
        Guid conversationId,
        int maxMessages,
        CancellationToken cancellationToken)
    {
        if (maxMessages < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxMessages),
                "The history limit cannot be negative.");
        }

        if (maxMessages == 0)
        {
            return [];
        }

        var messages = await db.Messages
            .AsNoTracking()
            .Where(item => item.ConversationId == conversationId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenByDescending(item => item.Id)
            .Take(maxMessages)
            .Select(item => new ChatMessage(item.Role, item.Content))
            .ToListAsync(cancellationToken);

        messages.Reverse();
        return messages;
    }

    public async Task<MessageEntity> AddUserMessageAsync(
        Guid conversationId,
        string content,
        CancellationToken cancellationToken)
    {
        return await AddMessageAsync(
            conversationId,
            role: "user",
            content: content.Trim(),
            decision: null,
            cancellationToken);
    }

    public async Task<MessageEntity> AddAssistantMessageAsync(
        Guid conversationId,
        AvatarDecision decision,
        CancellationToken cancellationToken)
    {
        return await AddMessageAsync(
            conversationId,
            role: "assistant",
            content: decision.Speech.Trim(),
            decision,
            cancellationToken);
    }

    public async Task SaveLlmTelemetryAsync(
        Guid messageId,
        ModelTelemetry telemetry,
        CancellationToken cancellationToken)
    {
        await EnsureAssistantMessageExistsAsync(messageId, cancellationToken);

        var entity = await db.LlmTelemetry
            .SingleOrDefaultAsync(
                item => item.MessageId == messageId,
                cancellationToken);

        if (entity is null)
        {
            entity = new LlmTelemetryEntity
            {
                MessageId = messageId,
            };
            db.LlmTelemetry.Add(entity);
        }

        entity.Model = telemetry.Model;
        entity.TotalDurationMs = telemetry.TotalDurationMs;
        entity.LoadDurationMs = telemetry.LoadDurationMs;
        entity.PromptTokens = telemetry.PromptTokens;
        entity.OutputTokens = telemetry.OutputTokens;
        entity.CreatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> AssistantMessageExistsAsync(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        return await db.Messages.AnyAsync(
            item => item.Id == messageId && item.Role == "assistant",
            cancellationToken);
    }

    public async Task SaveTtsTelemetryAsync(
        Guid messageId,
        SpeechSynthesisResult result,
        CancellationToken cancellationToken)
    {
        await EnsureAssistantMessageExistsAsync(messageId, cancellationToken);

        var entity = await db.TtsTelemetry
            .SingleOrDefaultAsync(
                item => item.MessageId == messageId,
                cancellationToken);

        if (entity is null)
        {
            entity = new TtsTelemetryEntity
            {
                MessageId = messageId,
            };
            db.TtsTelemetry.Add(entity);
        }

        entity.Model = result.Model;
        entity.VoiceSource = result.VoiceSource;
        entity.SynthesisDurationMs = result.SynthesisDurationMs;
        entity.AudioDurationMs = null;
        entity.RealTimeFactor = null;
        entity.UsedCuda = result.UsedCuda;
        entity.UsedFp16 = result.UsedFp16;
        entity.CreatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<MessageEntity> AddMessageAsync(
        Guid conversationId,
        string role,
        string content,
        AvatarDecision? decision,
        CancellationToken cancellationToken)
    {
        var conversation = await db.Conversations.FindAsync(
            [conversationId],
            cancellationToken);

        if (conversation is null)
        {
            throw new KeyNotFoundException(
                $"Conversation '{conversationId}' was not found in the local database.");
        }

        var now = DateTime.UtcNow;
        var message = new MessageEntity
        {
            ConversationId = conversationId,
            Role = role,
            Content = content,
            Emotion = decision?.Emotion,
            EmotionIntensity = decision?.EmotionIntensity,
            Gesture = decision?.Gesture,
            GestureIntensity = decision?.GestureIntensity,
            CreatedAtUtc = now,
        };

        conversation.UpdatedAtUtc = now;
        db.Messages.Add(message);
        await db.SaveChangesAsync(cancellationToken);
        return message;
    }

    private async Task EnsureAssistantMessageExistsAsync(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        if (!await AssistantMessageExistsAsync(messageId, cancellationToken))
        {
            throw new KeyNotFoundException(
                $"Assistant message '{messageId}' was not found in the local database.");
        }
    }

    private static string BuildConversationTitle(string firstUserMessage)
    {
        var title = firstUserMessage
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ')
            .Trim();

        while (title.Contains("  ", StringComparison.Ordinal))
        {
            title = title.Replace("  ", " ", StringComparison.Ordinal);
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return "New conversation";
        }

        return title.Length <= MaxConversationTitleLength
            ? title
            : $"{title[..(MaxConversationTitleLength - 1)]}…";
    }
}
