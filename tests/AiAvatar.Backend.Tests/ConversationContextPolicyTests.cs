using AiAvatar.Backend.Models;
using AiAvatar.Backend.Options;
using AiAvatar.Backend.Services;
using AiAvatar.Backend.Services.Ollama;

namespace AiAvatar.Backend.Tests;

public sealed class ConversationContextPolicyTests
{
    [Fact]
    public void CurrentUserBudget_RejectsLargeMultibyteMessageBeforeHistorySelection()
    {
        var policy = CreatePolicy();
        var request = new ChatRequest(
            ConversationId: null,
            TurnId: Guid.NewGuid(),
            Message: new string('你', 2_000));

        // The old fixed 4,000-character rule alone would accept this request.
        Assert.Null(ChatRequestValidator.Validate(request));

        var error = ChatRequestValidator.Validate(request, policy);

        Assert.NotNull(error);
        Assert.Contains("context window", error, StringComparison.OrdinalIgnoreCase);
        Assert.False(policy.CanFitCurrentUserMessage(request.Message));
    }

    [Fact]
    public void FitsConfiguredContext_AccountsForSystemPromptOutputAndSafetyReserve()
    {
        var policy = CreatePolicy();
        IReadOnlyList<ChatMessage> context =
        [
            new("user", "hello"),
            new("assistant", "hi"),
            new("user", "current"),
        ];

        Assert.True(policy.FitsConfiguredContext(context));
        Assert.True(policy.SystemPromptTokenEstimate > 0);
        Assert.Equal(280, policy.MaxOutputTokens);
        Assert.Equal(128, policy.ContextSafetyReserveTokens);
        Assert.True(
            policy.EstimateInputTokens(context) + policy.MaxOutputTokens <= policy.ContextLength);
    }

    private static ConversationContextPolicy CreatePolicy() =>
        new(
            Microsoft.Extensions.Options.Options.Create(new OllamaOptions
            {
                Model = "test-model",
                ContextLength = 4096,
                MaxOutputTokens = 280,
                ContextSafetyReserveTokens = 128,
            }),
            Microsoft.Extensions.Options.Options.Create(new CharacterOptions()));
}
