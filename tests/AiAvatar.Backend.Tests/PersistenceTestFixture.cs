using AiAvatar.Backend.Data;
using AiAvatar.Backend.Data.Entities;
using AiAvatar.Backend.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AiAvatar.Backend.Tests;

internal sealed class PersistenceTestFixture : IAsyncDisposable
{
    private PersistenceTestFixture(SqliteConnection connection, AvatarDbContext db)
    {
        Connection = connection;
        Db = db;
    }

    public SqliteConnection Connection { get; }

    public AvatarDbContext Db { get; }

    public static async Task<PersistenceTestFixture> CreateAsync(
        CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<AvatarDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new AvatarDbContext(options);
        await db.Database.EnsureCreatedAsync(cancellationToken);
        return new PersistenceTestFixture(connection, db);
    }

    public static MessageEntity Message(
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

    public static OllamaDecisionResult Result(
        string speech,
        string language = "en") =>
        new(
            new AvatarDecision(speech, language, "happy", 0.6, "nod", 0.2),
            new ModelTelemetry("test-model", 10, 1, 20, 5));

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await Connection.DisposeAsync();
    }
}
