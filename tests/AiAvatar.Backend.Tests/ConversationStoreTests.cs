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
    public async Task BuildModelContextAsync_UsesFiveCompleteTurnsAndNeverStartsWithAssistant()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await SqliteFixture.CreateAsync(cancellationToken);
        var conversation = new ConversationEntity { Title = "long history" };
        var start = DateTime.UtcNow.AddMinutes(-10);
        fixture.Db.Conversations.Add(conversation);

        for (var turn = 1; turn <= 7; turn++)
        {
            var turnId = Guid.NewGuid();
            var turnStart = start.AddSeconds(turn * 2);
            fixture.Db.Messages.AddRange(
                Message(conversation.Id, turnId, "user", $"user {turn}", turnStart),
                Message(conversation.Id, turnId, "assistant", $"assistant {turn}", turnStart.AddTicks(1)));
        }

        await fixture.Db.SaveChangesAsync(cancellationToken);
        var store = new ConversationStore(fixture.Db);

        var context = await store.BuildModelContextAsync(
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
        await using var fixture = await SqliteFixture.CreateAsync(cancellationToken);
        var conversation = new ConversationEntity { Title = "incomplete history" };
        var start = DateTime.UtcNow.AddMinutes(-5);
        fixture.Db.Conversations.Add(conversation);

        for (var turn = 1; turn <= 5; turn++)
        {
            var turnId = Guid.NewGuid();
            var turnStart = start.AddSeconds(turn * 2);
            fixture.Db.Messages.AddRange(
                Message(conversation.Id, turnId, "user", $"user {turn}", turnStart),
                Message(conversation.Id, turnId, "assistant", $"assistant {turn}", turnStart.AddTicks(1)));
        }

        fixture.Db.Messages.Add(Message(
            conversation.Id,
            Guid.NewGuid(),
            "assistant",
            "orphan assistant",
            DateTime.UtcNow));

        await fixture.Db.SaveChangesAsync(cancellationToken);
        var store = new ConversationStore(fixture.Db);

        var context = await store.BuildModelContextAsync(
            conversation.Id,
            "current user",
            cancellationToken);

        Assert.Equal(11, context.Count);
        Assert.DoesNotContain(context, message => message.Content == "orphan assistant");
        Assert.Equal("user", context[0].Role);
        Assert.Equal(new ChatMessage("user", "current user"), context[^1]);
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
                cancellationToken));
    }

    [Fact]
    public async Task GetAssistantSpeechSourceAsync_ReturnsPersistedContentAndEmotion()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await SqliteFixture.CreateAsync(cancellationToken);
        var conversation = new ConversationEntity { Title = "speech authority" };
        fixture.Db.Conversations.Add(conversation);

        var assistant = new MessageEntity
        {
            ConversationId = conversation.Id,
            TurnId = Guid.NewGuid(),
            Role = "assistant",
            Content = "persisted authoritative speech",
            Language = "en",
            Emotion = "sad",
            EmotionIntensity = 0.75,
        };
        fixture.Db.Messages.Add(assistant);
        await fixture.Db.SaveChangesAsync(cancellationToken);

        var store = new ConversationStore(fixture.Db);
        var source = await store.GetAssistantSpeechSourceAsync(
            assistant.Id,
            cancellationToken);

        Assert.NotNull(source);
        Assert.Equal("persisted authoritative speech", source.Text);
        Assert.Equal("sad", source.Emotion);
        Assert.Equal(0.75, source.EmotionIntensity);
    }

    private static MessageEntity Message(
        Guid conversationId,
        Guid turnId,
        string role,
        string content,
        DateTime createdAtUtc) =>
        new()
        {
            ConversationId = conversationId,
            TurnId = turnId,
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
