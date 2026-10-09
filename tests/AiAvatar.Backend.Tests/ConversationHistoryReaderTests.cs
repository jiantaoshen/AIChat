using AiAvatar.Backend.Data.Entities;
using AiAvatar.Backend.Errors;
using AiAvatar.Backend.Models;
using AiAvatar.Backend.Services.Persistence;

namespace AiAvatar.Backend.Tests;

public sealed class ConversationHistoryReaderTests
{
    [Fact]
    public async Task BuildModelContextAsync_UsesFiveCompleteTurnsAndNeverStartsWithAssistant()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await PersistenceTestFixture.CreateAsync(cancellationToken);
        var conversation = new ConversationEntity { Title = "long history" };
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
        var reader = new ConversationHistoryReader(fixture.Db);

        var context = await reader.BuildModelContextAsync(
            conversation.Id,
            "  current user  ",
            cancellationToken);

        Assert.Equal(11, context.Count);
        Assert.Equal(new ChatMessage("user", "user 3"), context[0]);
        Assert.Equal(new ChatMessage("assistant", "assistant 3"), context[1]);
        Assert.Equal(new ChatMessage("user", "current user"), context[^1]);

        for (var index = 0; index < 10; index += 2)
        {
            Assert.Equal("user", context[index].Role);
            Assert.Equal("assistant", context[index + 1].Role);
        }
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
        var reader = new ConversationHistoryReader(fixture.Db);

        var context = await reader.BuildModelContextAsync(
            conversation.Id,
            "current user",
            cancellationToken);

        Assert.Equal(11, context.Count);
        Assert.DoesNotContain(context, message => message.Content == "orphan assistant");
        Assert.Equal("user", context[0].Role);
        Assert.Equal(new ChatMessage("user", "current user"), context[^1]);
    }

    [Fact]
    public async Task BuildModelContextAsync_UnknownConversationIsNotFound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await PersistenceTestFixture.CreateAsync(cancellationToken);
        var reader = new ConversationHistoryReader(fixture.Db);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            reader.BuildModelContextAsync(
                Guid.NewGuid(),
                "hello",
                cancellationToken));
    }
}
