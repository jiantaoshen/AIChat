using AiAvatar.Backend.Data.Entities;
using AiAvatar.Backend.Errors;
using AiAvatar.Backend.Models;
using AiAvatar.Backend.Options;
using AiAvatar.Backend.Services.Ollama;
using AiAvatar.Backend.Services.Persistence;

namespace AiAvatar.Backend.Tests;

public sealed class ConversationHistoryReaderTests
{
    [Fact]
    public async Task BuildModelContextAsync_ShortTurnsCanUseMoreThanFiveCompleteTurns()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await PersistenceTestFixture.CreateAsync(cancellationToken);
        var conversation = new ConversationEntity { Title = "short history" };
        var start = DateTime.UtcNow.AddMinutes(-10);
        fixture.Db.Conversations.Add(conversation);

        for (var turn = 1; turn <= 7; turn++)
        {
            var turnId = Guid.NewGuid();
            var turnStart = start.AddSeconds(turn * 2);
            fixture.Db.Messages.AddRange(
                PersistenceTestFixture.Message(
                    conversation.Id,
                    turnId,
                    "user",
                    $"user {turn}",
                    turnStart),
                PersistenceTestFixture.Message(
                    conversation.Id,
                    turnId,
                    "assistant",
                    $"assistant {turn}",
                    turnStart.AddTicks(1)));
        }

        await fixture.Db.SaveChangesAsync(cancellationToken);
        var policy = CreatePolicy();
        var reader = new ConversationHistoryReader(fixture.Db, policy);

        var context = await reader.BuildModelContextAsync(
            conversation.Id,
            "  current user  ",
            cancellationToken);

        Assert.Equal(15, context.Count);
        Assert.Equal(new ChatMessage("user", "user 1"), context[0]);
        Assert.Equal(new ChatMessage("assistant", "assistant 1"), context[1]);
        Assert.Equal(new ChatMessage("user", "current user"), context[^1]);
        Assert.True(policy.FitsConfiguredContext(context));

        for (var index = 0; index < context.Count - 1; index += 2)
        {
            Assert.Equal("user", context[index].Role);
            Assert.Equal("assistant", context[index + 1].Role);
        }
    }

    [Fact]
    public async Task BuildModelContextAsync_VeryLargeConversationStaysWithinConfiguredContextBudget()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await PersistenceTestFixture.CreateAsync(cancellationToken);
        var conversation = new ConversationEntity { Title = "large history" };
        var start = DateTime.UtcNow.AddMinutes(-30);
        fixture.Db.Conversations.Add(conversation);

        for (var turn = 1; turn <= 20; turn++)
        {
            var turnId = Guid.NewGuid();
            var turnStart = start.AddSeconds(turn * 2);
            fixture.Db.Messages.AddRange(
                PersistenceTestFixture.Message(
                    conversation.Id,
                    turnId,
                    "user",
                    $"turn-{turn}:" + new string('你', 240),
                    turnStart),
                PersistenceTestFixture.Message(
                    conversation.Id,
                    turnId,
                    "assistant",
                    $"turn-{turn}:" + new string('好', 180),
                    turnStart.AddTicks(1)));
        }

        await fixture.Db.SaveChangesAsync(cancellationToken);
        var policy = CreatePolicy();
        var reader = new ConversationHistoryReader(fixture.Db, policy);

        var context = await reader.BuildModelContextAsync(
            conversation.Id,
            "current user",
            cancellationToken);

        Assert.True(policy.FitsConfiguredContext(context));
        Assert.True(
            policy.EstimateInputTokens(context) + policy.MaxOutputTokens <= policy.ContextLength);
        Assert.Equal(new ChatMessage("user", "current user"), context[^1]);

        // The boundary must remain turn-aware even when the token budget, not a fixed turn
        // count, determines where history stops.
        Assert.Equal(0, (context.Count - 1) % 2);
        for (var index = 0; index < context.Count - 1; index += 2)
        {
            Assert.Equal("user", context[index].Role);
            Assert.Equal("assistant", context[index + 1].Role);
        }

        // These deliberately large turns prove the reader does not blindly include five.
        Assert.True(context.Count < 11);
        Assert.StartsWith("turn-20:", context[0].Content);
    }

    [Fact]
    public async Task BuildModelContextAsync_IgnoresIncompleteTurnInsteadOfCuttingAtMessageBoundary()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await PersistenceTestFixture.CreateAsync(cancellationToken);
        var conversation = new ConversationEntity { Title = "incomplete history" };
        var start = DateTime.UtcNow.AddMinutes(-5);
        fixture.Db.Conversations.Add(conversation);

        for (var turn = 1; turn <= 5; turn++)
        {
            var turnId = Guid.NewGuid();
            var turnStart = start.AddSeconds(turn * 2);
            fixture.Db.Messages.AddRange(
                PersistenceTestFixture.Message(
                    conversation.Id,
                    turnId,
                    "user",
                    $"user {turn}",
                    turnStart),
                PersistenceTestFixture.Message(
                    conversation.Id,
                    turnId,
                    "assistant",
                    $"assistant {turn}",
                    turnStart.AddTicks(1)));
        }

        fixture.Db.Messages.Add(PersistenceTestFixture.Message(
            conversation.Id,
            Guid.NewGuid(),
            "assistant",
            "orphan assistant",
            DateTime.UtcNow));

        await fixture.Db.SaveChangesAsync(cancellationToken);
        var policy = CreatePolicy();
        var reader = new ConversationHistoryReader(fixture.Db, policy);

        var context = await reader.BuildModelContextAsync(
            conversation.Id,
            "current user",
            cancellationToken);

        Assert.DoesNotContain(context, message => message.Content == "orphan assistant");
        Assert.Equal("user", context[0].Role);
        Assert.Equal(new ChatMessage("user", "current user"), context[^1]);
        Assert.True(policy.FitsConfiguredContext(context));
    }

    [Fact]
    public async Task BuildModelContextAsync_UnknownConversationIsNotFound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await PersistenceTestFixture.CreateAsync(cancellationToken);
        var reader = new ConversationHistoryReader(fixture.Db, CreatePolicy());

        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            reader.BuildModelContextAsync(
                Guid.NewGuid(),
                "hello",
                cancellationToken));
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
