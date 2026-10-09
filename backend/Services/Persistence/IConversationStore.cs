// This interface defines SQLite operations for backend-owned chat history, atomic chat turns, idempotency, and TTS telemetry.
using AiAvatar.Backend.Models;

namespace AiAvatar.Backend.Services.Persistence;

public interface IConversationStore
{
    Task<AvatarChatResponse?> TryGetCompletedTurnAsync(
        Guid turnId,
        Guid? requestedConversationId,
        string userMessage,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ChatMessage>> BuildModelContextAsync(
        Guid? conversationId,
        string currentUserMessage,
        CancellationToken cancellationToken);

    Task<AvatarChatResponse> CommitTurnAsync(
        Guid? conversationId,
        Guid turnId,
        string userMessage,
        OllamaDecisionResult ollamaResult,
        CancellationToken cancellationToken);

    Task<AssistantSpeechSource?> GetAssistantSpeechSourceAsync(
        Guid messageId,
        CancellationToken cancellationToken);

    Task SaveTtsTelemetryAsync(
        Guid messageId,
        SpeechSynthesisResult result,
        CancellationToken cancellationToken);
}
