// This policy owns deterministic, conservative budgeting for the model input context.
using System.Text;
using AiAvatar.Backend.Models;
using AiAvatar.Backend.Options;
using Microsoft.Extensions.Options;

namespace AiAvatar.Backend.Services.Ollama;

public sealed class ConversationContextPolicy
{
    // Each chat message also consumes role markers / template delimiters that are not part
    // of Content. This is intentionally conservative rather than tokenizer-specific.
    internal const int MessageFramingReserveTokens = 16;

    private readonly OllamaOptions _ollama;
    private readonly int _systemPromptTokens;

    public ConversationContextPolicy(
        IOptions<OllamaOptions> ollamaOptions,
        IOptions<CharacterOptions> characterOptions)
    {
        _ollama = ollamaOptions.Value;

        var systemPrompt = AvatarSystemPrompt.Build(characterOptions.Value);
        _systemPromptTokens = EstimateConservativeTextTokens(systemPrompt) + MessageFramingReserveTokens;

        if (ConversationMessageBudgetTokens <= 0)
        {
            throw new InvalidOperationException(
                "The configured Ollama context leaves no room for conversation messages after " +
                "reserving output tokens, system prompt tokens, and the context safety margin.");
        }
    }

    public int ContextLength => _ollama.ContextLength;

    public int MaxOutputTokens => _ollama.MaxOutputTokens;

    public int ContextSafetyReserveTokens => _ollama.ContextSafetyReserveTokens;

    public int SystemPromptTokenEstimate => _systemPromptTokens;

    // num_ctx must cover both prompt/input and generated output. The model-input side is
    // therefore bounded by context length minus the configured output reserve.
    public int InputTokenLimit => ContextLength - MaxOutputTokens;

    public int ConversationMessageBudgetTokens =>
        InputTokenLimit - ContextSafetyReserveTokens - SystemPromptTokenEstimate;

    public bool CanFitCurrentUserMessage(string currentUserMessage) =>
        EstimateMessageTokens("user", currentUserMessage.Trim()) <= ConversationMessageBudgetTokens;

    public int GetHistoryBudgetTokens(string currentUserMessage)
    {
        var currentUserCost = EstimateMessageTokens("user", currentUserMessage.Trim());
        return Math.Max(0, ConversationMessageBudgetTokens - currentUserCost);
    }

    public int EstimateCompleteTurnTokens(string userContent, string assistantContent) =>
        EstimateMessageTokens("user", userContent) +
        EstimateMessageTokens("assistant", assistantContent);

    public int EstimateMessageTokens(ChatMessage message) =>
        EstimateMessageTokens(message.Role, message.Content);

    public int EstimateInputTokens(IEnumerable<ChatMessage> messages)
    {
        var total = ContextSafetyReserveTokens + SystemPromptTokenEstimate;
        foreach (var message in messages)
        {
            total += EstimateMessageTokens(message);
        }

        return total;
    }

    public bool FitsConfiguredContext(IEnumerable<ChatMessage> messages) =>
        EstimateInputTokens(messages) + MaxOutputTokens <= ContextLength;

    public int GetMaxCandidateCompletedTurns(string currentUserMessage)
    {
        var historyBudget = GetHistoryBudgetTokens(currentUserMessage);
        var minimumCompleteTurnCost = MessageFramingReserveTokens * 2;
        return historyBudget / minimumCompleteTurnCost;
    }

    private static int EstimateMessageTokens(string role, string content)
    {
        // Use a deliberately pessimistic UTF-8-byte proxy instead of an optimistic
        // characters-per-token ratio. This strongly over-reserves many inputs, especially
        // Chinese and other multibyte scripts, but keeps budgeting deterministic until an
        // exact Qwen tokenizer is introduced. Message framing is reserved separately.
        _ = role; // The role marker itself is covered by the framing reserve above.
        return EstimateConservativeTextTokens(content) + MessageFramingReserveTokens;
    }

    private static int EstimateConservativeTextTokens(string text) =>
        Encoding.UTF8.GetByteCount(text);
}
