// This entity stores one user or assistant message together with optional avatar semantics and its idempotent chat-turn identity.
namespace AiAvatar.Backend.Data.Entities;

public sealed class MessageEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ConversationId { get; set; }

    public ConversationEntity Conversation { get; set; } = null!;

    public Guid? TurnId { get; set; }

    public string Role { get; set; } = "";

    public string Content { get; set; } = "";

    public string? Language { get; set; }

    public string? Emotion { get; set; }

    public double? EmotionIntensity { get; set; }

    public string? Gesture { get; set; }

    public double? GestureIntensity { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public LlmTelemetryEntity? LlmTelemetry { get; set; }

    public TtsTelemetryEntity? TtsTelemetry { get; set; }
}
