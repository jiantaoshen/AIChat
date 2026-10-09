// This service owns backend conversation history and atomically persists completed chat turns plus speech telemetry through EF Core SQLite.
using AiAvatar.Backend.Data;
using AiAvatar.Backend.Data.Entities;
using AiAvatar.Backend.Errors;
using AiAvatar.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace AiAvatar.Backend.Services.Persistence;

public sealed class ConversationStore(AvatarDbContext db) : IConversationStore
{
    private const int MaxConversationTitleLength = 80;
    private const int MaxCompletedContextTurns = 5;

    public async Task<AvatarChatResponse?> TryGetCompletedTurnAsync(
        Guid turnId,
        Guid? requestedConversationId,
        string userMessage,
        CancellationToken cancellationToken)
    {
        var user = await db.Messages
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.TurnId == turnId && item.Role == "user",
                cancellationToken);

        if (user is null)
        {
            return null;
        }

        EnsureTurnMatchesRequest(user, requestedConversationId, userMessage);

        var assistant = await db.Messages
            .AsNoTracking()
            .Include(item => item.LlmTelemetry)
            .SingleOrDefaultAsync(
                item => item.TurnId == turnId && item.Role == "assistant",
                cancellationToken);

        if (assistant?.LlmTelemetry is null)
        {
            throw new InvalidOperationException(
                $"Chat turn '{turnId}' is incomplete in the local database.");
        }

        return BuildResponse(assistant);
    }

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

    public async Task<AvatarChatResponse> CommitTurnAsync(
        Guid? conversationId,
        Guid turnId,
        string userMessage,
        OllamaDecisionResult ollamaResult,
        CancellationToken cancellationToken)
    {
        var existing = await TryGetCompletedTurnAsync(
            turnId,
            conversationId,
            userMessage,
            cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var conversation = await GetOrCreateConversationForCommitAsync(
                conversationId,
                userMessage,
                cancellationToken);

            var now = DateTime.UtcNow;
            var decision = ollamaResult.Decision with
            {
                Speech = ollamaResult.Decision.Speech.Trim(),
            };
            var user = new MessageEntity
            {
                ConversationId = conversation.Id,
                TurnId = turnId,
                Role = "user",
                Content = userMessage.Trim(),
                CreatedAtUtc = now,
            };

            var assistant = new MessageEntity
            {
                ConversationId = conversation.Id,
                TurnId = turnId,
                Role = "assistant",
                Content = decision.Speech,
                Language = decision.Language,
                Emotion = decision.Emotion,
                EmotionIntensity = decision.EmotionIntensity,
                Gesture = decision.Gesture,
                GestureIntensity = decision.GestureIntensity,
                CreatedAtUtc = now.AddTicks(1),
            };

            var telemetry = new LlmTelemetryEntity
            {
                MessageId = assistant.Id,
                Message = assistant,
                Model = ollamaResult.Telemetry.Model,
                TotalDurationMs = ollamaResult.Telemetry.TotalDurationMs,
                LoadDurationMs = ollamaResult.Telemetry.LoadDurationMs,
                PromptTokens = ollamaResult.Telemetry.PromptTokens,
                OutputTokens = ollamaResult.Telemetry.OutputTokens,
                CreatedAtUtc = now.AddTicks(1),
            };

            conversation.UpdatedAtUtc = assistant.CreatedAtUtc;
            db.Messages.AddRange(user, assistant);
            db.LlmTelemetry.Add(telemetry);

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new AvatarChatResponse(
                conversation.Id,
                assistant.Id,
                decision,
                ollamaResult.Telemetry);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();

            var winner = await TryGetCompletedTurnAsync(
                turnId,
                conversationId,
                userMessage,
                cancellationToken);
            if (winner is not null)
            {
                return winner;
            }

            throw;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<AssistantSpeechSource?> GetAssistantSpeechSourceAsync(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        return await db.Messages
            .AsNoTracking()
            .Where(item => item.Id == messageId && item.Role == "assistant")
            .Select(item => new AssistantSpeechSource(
                item.Content,
                item.Emotion,
                item.EmotionIntensity))
            .SingleOrDefaultAsync(cancellationToken);
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

    private async Task<ConversationEntity> GetOrCreateConversationForCommitAsync(
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
                throw new ResourceNotFoundException(
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
        return conversation;
    }

    private async Task EnsureAssistantMessageExistsAsync(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        var exists = await db.Messages.AnyAsync(
            item => item.Id == messageId && item.Role == "assistant",
            cancellationToken);

        if (!exists)
        {
            throw new ResourceNotFoundException(
                $"Assistant message '{messageId}' was not found in the local database.");
        }
    }

    private static void EnsureTurnMatchesRequest(
        MessageEntity user,
        Guid? requestedConversationId,
        string userMessage)
    {
        if (requestedConversationId is Guid requested &&
            requested != user.ConversationId)
        {
            throw new ChatTurnConflictException(
                $"TurnId '{user.TurnId}' already belongs to conversation '{user.ConversationId}'.");
        }

        if (!string.Equals(user.Content, userMessage.Trim(), StringComparison.Ordinal))
        {
            throw new ChatTurnConflictException(
                $"TurnId '{user.TurnId}' was already used for a different message.");
        }
    }

    private static AvatarChatResponse BuildResponse(MessageEntity assistant)
    {
        var telemetry = assistant.LlmTelemetry
            ?? throw new InvalidOperationException(
                $"Assistant message '{assistant.Id}' has no LLM telemetry.");

        return new AvatarChatResponse(
            assistant.ConversationId,
            assistant.Id,
            new AvatarDecision(
                assistant.Content,
                assistant.Language ?? "und",
                assistant.Emotion ?? "neutral",
                assistant.EmotionIntensity ?? 0,
                assistant.Gesture ?? "none",
                assistant.GestureIntensity ?? 0),
            new ModelTelemetry(
                telemetry.Model,
                telemetry.TotalDurationMs,
                telemetry.LoadDurationMs,
                telemetry.PromptTokens,
                telemetry.OutputTokens));
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
