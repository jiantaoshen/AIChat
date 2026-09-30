// This entity represents one persistent local conversation stored in SQLite.
namespace AiAvatar.Backend.Data.Entities;

public sealed class ConversationEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = "New conversation";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<MessageEntity> Messages { get; set; } = [];
}
