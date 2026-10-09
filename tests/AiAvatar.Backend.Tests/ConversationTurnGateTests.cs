using AiAvatar.Backend.Services.Concurrency;

namespace AiAvatar.Backend.Tests;

public sealed class ConversationTurnGateTests
{
    [Fact]
    public async Task SameConversation_SecondLeaseWaitsUntilFirstLeaseIsReleased()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var gate = new ConversationTurnGate();
        var conversationId = Guid.NewGuid();

        var first = await gate.AcquireAsync(
            conversationId,
            Guid.NewGuid(),
            cancellationToken);

        var secondLeaseTask = gate.AcquireAsync(
            conversationId,
            Guid.NewGuid(),
            cancellationToken).AsTask();

        Assert.False(secondLeaseTask.IsCompleted);

        first.Dispose();
        using var second = await secondLeaseTask;
    }

    [Fact]
    public async Task DifferentConversations_DoNotBlockEachOther()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var gate = new ConversationTurnGate();

        using var first = await gate.AcquireAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            cancellationToken);

        var secondLeaseTask = gate.AcquireAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            cancellationToken).AsTask();

        Assert.True(secondLeaseTask.IsCompletedSuccessfully);
        using var second = await secondLeaseTask;
    }

    [Fact]
    public async Task SameNewTurnId_SerializesConcurrentConversationCreationRetries()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var gate = new ConversationTurnGate();
        var turnId = Guid.NewGuid();

        var first = await gate.AcquireAsync(
            conversationId: null,
            turnId,
            cancellationToken);

        var retryLeaseTask = gate.AcquireAsync(
            conversationId: null,
            turnId,
            cancellationToken).AsTask();

        Assert.False(retryLeaseTask.IsCompleted);

        first.Dispose();
        using var retry = await retryLeaseTask;
    }
}
