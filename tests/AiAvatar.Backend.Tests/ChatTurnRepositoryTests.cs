using AiAvatar.Backend.Errors;
using AiAvatar.Backend.Services.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiAvatar.Backend.Tests;

public sealed class ChatTurnRepositoryTests
{
    [Fact]
    public async Task CommitTurnAsync_RollsBackConversationMessagesAndTelemetryWhenSaveFails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await PersistenceTestFixture.CreateAsync(cancellationToken);
        await fixture.Db.Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER FailLlmTelemetryInsert
            BEFORE INSERT ON LlmTelemetry
            BEGIN
                SELECT RAISE(ABORT, 'forced telemetry failure');
            END;
            """,
            Array.Empty<object>(),
            cancellationToken);
        var repository = new ChatTurnRepository(fixture.Db);

        await Assert.ThrowsAsync<DbUpdateException>(() => repository.CommitTurnAsync(
            conversationId: null,
            turnId: Guid.NewGuid(),
            userMessage: "hello",
            ollamaResult: PersistenceTestFixture.Result("assistant"),
            cancellationToken));

        Assert.Equal(0, await fixture.Db.Conversations.CountAsync(cancellationToken));
        Assert.Equal(0, await fixture.Db.Messages.CountAsync(cancellationToken));
        Assert.Equal(0, await fixture.Db.LlmTelemetry.CountAsync(cancellationToken));
    }

    [Fact]
    public async Task CommitTurnAsync_SameTurnRetryReturnsOriginalResultWithoutDuplicates()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await PersistenceTestFixture.CreateAsync(cancellationToken);
        var repository = new ChatTurnRepository(fixture.Db);
        var turnId = Guid.NewGuid();
        var first = await repository.CommitTurnAsync(
            null,
            turnId,
            "hello",
            PersistenceTestFixture.Result("first answer", language: "zh"),
            cancellationToken);

        var retry = await repository.CommitTurnAsync(
            null,
            turnId,
            "hello",
            PersistenceTestFixture.Result(
                "different answer that must not win",
                language: "en"),
            cancellationToken);

        Assert.Equal(first.ConversationId, retry.ConversationId);
        Assert.Equal(first.AssistantMessageId, retry.AssistantMessageId);
        Assert.Equal("first answer", retry.Decision.Speech);
        Assert.Equal("zh", retry.Decision.Language);
        Assert.Equal(1, await fixture.Db.Conversations.CountAsync(cancellationToken));
        Assert.Equal(2, await fixture.Db.Messages.CountAsync(cancellationToken));
        Assert.Equal(1, await fixture.Db.LlmTelemetry.CountAsync(cancellationToken));
    }

    [Fact]
    public async Task TryGetCompletedTurnAsync_ReusingTurnIdForDifferentMessageIsConflict()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await PersistenceTestFixture.CreateAsync(cancellationToken);
        var repository = new ChatTurnRepository(fixture.Db);
        var turnId = Guid.NewGuid();
        await repository.CommitTurnAsync(
            null,
            turnId,
            "original",
            PersistenceTestFixture.Result("answer"),
            cancellationToken);

        await Assert.ThrowsAsync<ChatTurnConflictException>(() =>
            repository.TryGetCompletedTurnAsync(
                turnId,
                requestedConversationId: null,
                userMessage: "different",
                cancellationToken));
    }
}
