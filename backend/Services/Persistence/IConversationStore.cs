// This interface defines deterministic SQLite persistence operations used by chat and TTS endpoints.
using AiAvatar.Backend.Data.Entities;
using AiAvatar.Backend.Models;

namespace AiAvatar.Backend.Services.Persistence;

public interface IConversationStore
{
    Task<ConversationEntity> GetOrCreateConversationAsync(
        Guid? conversationId,
        string firstUserMessage,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ChatMessage>> GetRecentMessagesAsync(
        Guid conversationId,
        int maxMessages,
        CancellationToken cancellationToken);

    Task<MessageEntity> AddUserMessageAsync(
        Guid conversationId,
        string content,
        CancellationToken cancellationToken);

    Task<MessageEntity> AddAssistantMessageAsync(
        Guid conversationId,
        AvatarDecision decision,
        CancellationToken cancellationToken);

    Task SaveLlmTelemetryAsync(
        Guid messageId,
        ModelTelemetry telemetry,
        CancellationToken cancellationToken);

    Task<bool> AssistantMessageExistsAsync(
        Guid messageId,
        CancellationToken cancellationToken);

    Task SaveTtsTelemetryAsync(
        Guid messageId,
        SpeechSynthesisResult result,
        CancellationToken cancellationToken);
}
