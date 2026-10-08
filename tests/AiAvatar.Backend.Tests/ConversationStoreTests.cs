using AiAvatar.Backend.Data;
using AiAvatar.Backend.Data.Entities;
using AiAvatar.Backend.Errors;
using AiAvatar.Backend.Models;
using AiAvatar.Backend.Services.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AiAvatar.Backend.Tests;

public sealed class ConversationStoreTests
{
    [Fact]
    public async Task BuildModelContextAsync_LoadsBoundedPersistedHistoryAndAppendsCurrentIntent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await SqliteFixture.CreateAsync(cancellationToken);
        var conversation = new ConversationEntity { Title = "history" };
        var start = DateTime.UtcNow.AddMinutes(-5);
        fixture.Db.Conversations.Add(conversation);
        fixture.Db.Messages.AddRange(
            Message(conversation.Id, "user", "old user", start),
            Message(conversation.Id, "assistant", "old assistant", start.AddSeconds(1)),
            Message(conversation.Id, "user", "recent user", start.AddSeconds(2)),
            Message(conversation.Id, "assistant", "recent assistant", start.AddSeconds(3)));
        await fixture.Db.SaveChangesAsync(cancellationToken);
        var store = new ConversationStore(fixture.Db);

        var context = await store.BuildModelContextAsync(
            conversation.Id,
            "  current user  ",
            maxMessages: 3,
            cancellationToken);

        Assert.Equal(3, context.Count);
        Assert.Equal(new ChatMessage("user", "recent user"), context[0]);
        Assert.Equal(new ChatMessage("assistant", "recent assistant"), context[1]);
        Assert.Equal(new ChatMessage("user", "current user"), context[2]);
    }

    [Fact]
    public async Task CommitTurnAsync_RollsBackConversationMessagesAndTelemetryWhenSaveFails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await SqliteFixture.CreateAsync(cancellationToken);
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
        var store = new ConversationStore(fixture.Db);

        await Assert.ThrowsAsync<DbUpdateException>(() => store.CommitTurnAsync(
            conversationId: null,
            turnId: Guid.NewGuid(),
            userMessage: "hello",
            ollamaResult: Result("assistant"),
            cancellationToken));

        Assert.Equal(0, await fixture.Db.Conversations.CountAsync(cancellationToken));
        Assert.Equal(0, await fixture.Db.Messages.CountAsync(cancellationToken));
        Assert.Equal(0, await fixture.Db.LlmTelemetry.CountAsync(cancellationToken));
    }

    [Fact]
    public async Task CommitTurnAsync_SameTurnRetryReturnsOriginalResultWithoutDuplicates()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await SqliteFixture.CreateAsync(cancellationToken);
        var store = new ConversationStore(fixture.Db);
        var turnId = Guid.NewGuid();
        var first = await store.CommitTurnAsync(
            null,
            turnId,
            "hello",
            Result("first answer", language: "zh"),
            cancellationToken);

        var retry = await store.CommitTurnAsync(
            null,
            turnId,
            "hello",
            Result("different answer that must not win", language: "en"),
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
        await using var fixture = await SqliteFixture.CreateAsync(cancellationToken);
        var store = new ConversationStore(fixture.Db);
        var turnId = Guid.NewGuid();
        await store.CommitTurnAsync(
            null,
            turnId,
            "original",
            Result("answer"),
            cancellationToken);

        await Assert.ThrowsAsync<ChatTurnConflictException>(() =>
            store.TryGetCompletedTurnAsync(
                turnId,
                requestedConversationId: null,
                userMessage: "different",
                cancellationToken));
    }

    [Fact]
    public async Task BuildModelContextAsync_UnknownConversationIsNotFound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await SqliteFixture.CreateAsync(cancellationToken);
        var store = new ConversationStore(fixture.Db);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            store.BuildModelContextAsync(
                Guid.NewGuid(),
                "hello",
                4,
                cancellationToken));
    }

    private static MessageEntity Message(
        Guid conversationId,
        string role,
        string content,
        DateTime createdAtUtc) =>
        new()
        {
            ConversationId = conversationId,
            Role = role,
            Content = content,
            CreatedAtUtc = createdAtUtc,
        };

    private static OllamaDecisionResult Result(string speech, string language = "en") =>
        new(
            new AvatarDecision(speech, language, "happy", 0.6, "nod", 0.2),
            new ModelTelemetry("test-model", 10, 1, 20, 5));

    private sealed class SqliteFixture : IAsyncDisposable
    {
        private SqliteFixture(SqliteConnection connection, AvatarDbContext db)
        {
            Connection = connection;
            Db = db;
        }

        public SqliteConnection Connection { get; }

        public AvatarDbContext Db { get; }

        public static async Task<SqliteFixture> CreateAsync(CancellationToken cancellationToken)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync(cancellationToken);
            var options = new DbContextOptionsBuilder<AvatarDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new AvatarDbContext(options);
            await db.Database.EnsureCreatedAsync(cancellationToken);
            return new SqliteFixture(connection, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
