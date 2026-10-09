// This interface isolates chat-turn orchestration from the concrete Ollama HTTP transport and makes concurrency behavior deterministic to test.
using AiAvatar.Backend.Models;

namespace AiAvatar.Backend.Services.Ollama;

public interface IChatDecisionGenerator
{
    Task<OllamaDecisionResult> CreateDecisionAsync(
        IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken);
}
