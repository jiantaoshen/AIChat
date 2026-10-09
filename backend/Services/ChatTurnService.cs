// This service defines the chat-turn boundary: per-conversation serialization -> server-owned history -> Ollama -> one atomic, idempotent persistence commit.
using AiAvatar.Backend.Models;
using AiAvatar.Backend.Services.Concurrency;
using AiAvatar.Backend.Services.Ollama;
using AiAvatar.Backend.Services.Persistence;
using AiAvatar.Backend.Services.Speech;

namespace AiAvatar.Backend.Services;

public sealed class ChatTurnService(
    IChatDecisionGenerator decisionGenerator,
    IChatTurnRepository chatTurns,
    IConversationHistoryReader historyReader,
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

        var existing = await chatTurns.TryGetCompletedTurnAsync(
            request.TurnId,
            request.ConversationId,
            request.Message,
            cancellationToken);
        if (existing is not null)
        {
            return WithSpeechCapability(existing);
        }

        var context = await historyReader.BuildModelContextAsync(
            request.ConversationId,
            request.Message,
            cancellationToken);

        var ollamaResult = await decisionGenerator.CreateDecisionAsync(
            context,
            cancellationToken);

        var committed = await chatTurns.CommitTurnAsync(
            request.ConversationId,
            request.TurnId,
            request.Message,
            ollamaResult,
            cancellationToken);

        return WithSpeechCapability(committed);
    }

    private static AvatarChatResponse WithSpeechCapability(AvatarChatResponse response) =>
        response with
        {
            SpeechCapability = new SpeechCapability(
                TtsSpeechPolicy.IsSupportedLanguage(response.Decision.Language)),
        };
}
