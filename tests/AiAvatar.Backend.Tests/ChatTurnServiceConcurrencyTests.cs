using AiAvatar.Backend.Models;
using AiAvatar.Backend.Services;
using AiAvatar.Backend.Services.Concurrency;
using AiAvatar.Backend.Services.Ollama;
using AiAvatar.Backend.Services.Persistence;

namespace AiAvatar.Backend.Tests;

public sealed class ChatTurnServiceConcurrencyTests
{
    [Fact]
    public async Task SameConversation_SecondTurnWaitsAndSeesFirstCommittedTurn()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var conversationId = Guid.NewGuid();
        var persistence = new InMemoryChatPersistence(conversationId);
        var generator = new BlockingDecisionGenerator();
        var service = new ChatTurnService(
            generator,
            persistence,
            persistence,
            new ConversationTurnGate());

        var firstTask = service.ExecuteAsync(
            new ChatRequest(conversationId, Guid.NewGuid(), "first user"),
            cancellationToken);

        await generator.FirstCallStarted.Task.WaitAsync(cancellationToken);

        var secondExecuteReturned = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondTask = Task.Run(
            async () =>
            {
                var pending = service.ExecuteAsync(
                    new ChatRequest(conversationId, Guid.NewGuid(), "second user"),
                    cancellationToken);
                secondExecuteReturned.TrySetResult(true);
                return await pending;
            },
            cancellationToken);

        // Calling ExecuteAsync runs synchronously until its first incomplete await.
        // With the first conversation lease held, the second call must return a pending
        // Task at the gate and therefore cannot reach either history loading or Ollama.
        await secondExecuteReturned.Task.WaitAsync(cancellationToken);
        Assert.False(generator.SecondCallStarted.Task.IsCompleted);
        Assert.DoesNotContain(
            persistence.BuiltContexts,
            context => context.CurrentUserMessage == "second user");

        generator.ReleaseFirstCall();

        var firstResponse = await firstTask;
        var secondResponse = await secondTask;

        Assert.True(firstResponse.SpeechCapability.Supported);
        Assert.True(secondResponse.SpeechCapability.Supported);

        await generator.SecondCallStarted.Task.WaitAsync(cancellationToken);

        var secondContext = Assert.Single(
            persistence.BuiltContexts.Where(
                context => context.CurrentUserMessage == "second user"));

        Assert.Collection(
            secondContext.Messages,
            message => Assert.Equal(new ChatMessage("user", "first user"), message),
            message => Assert.Equal(new ChatMessage("assistant", "first assistant"), message),
            message => Assert.Equal(new ChatMessage("user", "second user"), message));
    }

    private sealed class BlockingDecisionGenerator : IChatDecisionGenerator
    {
        private readonly TaskCompletionSource<bool> _releaseFirst = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _callCount;

        public TaskCompletionSource<bool> FirstCallStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> SecondCallStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<OllamaDecisionResult> CreateDecisionAsync(
            IReadOnlyList<ChatMessage> messages,
            CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref _callCount);
            if (call == 1)
            {
                FirstCallStarted.TrySetResult(true);
                return CompleteFirstAsync(cancellationToken);
            }

            SecondCallStarted.TrySetResult(true);
            return Task.FromResult(Result("second assistant"));
        }

        public void ReleaseFirstCall() => _releaseFirst.TrySetResult(true);

        private async Task<OllamaDecisionResult> CompleteFirstAsync(
            CancellationToken cancellationToken)
        {
            await _releaseFirst.Task.WaitAsync(cancellationToken);
            return Result("first assistant");
        }

        private static OllamaDecisionResult Result(string speech) =>
            new(
                new AvatarDecision(speech, "en", "neutral", 0.2, "none", 0),
                new ModelTelemetry("test-model", 1, 0, 1, 1));
    }

    private sealed class InMemoryChatPersistence(Guid conversationId) :
        IChatTurnRepository,
        IConversationHistoryReader
    {
        private readonly object _sync = new();
        private readonly List<ChatMessage> _history = [];
        private readonly List<BuiltContext> _builtContexts = [];

        public IReadOnlyList<BuiltContext> BuiltContexts
        {
            get
            {
                lock (_sync)
                {
                    return _builtContexts.ToArray();
                }
            }
        }

        public Task<AvatarChatResponse?> TryGetCompletedTurnAsync(
            Guid turnId,
            Guid? requestedConversationId,
            string userMessage,
            CancellationToken cancellationToken) =>
            Task.FromResult<AvatarChatResponse?>(null);

        public Task<IReadOnlyList<ChatMessage>> BuildModelContextAsync(
            Guid? requestedConversationId,
            string currentUserMessage,
            CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                var messages = _history
                    .Append(new ChatMessage("user", currentUserMessage))
                    .ToArray();
                _builtContexts.Add(new BuiltContext(currentUserMessage, messages));
                return Task.FromResult<IReadOnlyList<ChatMessage>>(messages);
            }
        }

        public Task<AvatarChatResponse> CommitTurnAsync(
            Guid? requestedConversationId,
            Guid turnId,
            string userMessage,
            OllamaDecisionResult ollamaResult,
            CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                _history.Add(new ChatMessage("user", userMessage));
                _history.Add(new ChatMessage("assistant", ollamaResult.Decision.Speech));
            }

            return Task.FromResult(new AvatarChatResponse(
                conversationId,
                Guid.NewGuid(),
                ollamaResult.Decision,
                ollamaResult.Telemetry));
        }
    }

    public sealed record BuiltContext(
        string CurrentUserMessage,
        IReadOnlyList<ChatMessage> Messages);
}
