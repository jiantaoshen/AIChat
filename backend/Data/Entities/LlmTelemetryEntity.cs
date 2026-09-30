// This entity stores one Ollama/Qwen inference measurement for an assistant message.
namespace AiAvatar.Backend.Data.Entities;

public sealed class LlmTelemetryEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MessageId { get; set; }

    public MessageEntity Message { get; set; } = null!;

    public string Model { get; set; } = "";

    public double? TotalDurationMs { get; set; }

    public double? LoadDurationMs { get; set; }

    public int? PromptTokens { get; set; }

    public int? OutputTokens { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
