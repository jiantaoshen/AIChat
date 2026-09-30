// This entity stores one successful CosyVoice3 synthesis measurement for an assistant message.
namespace AiAvatar.Backend.Data.Entities;

public sealed class TtsTelemetryEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MessageId { get; set; }

    public MessageEntity Message { get; set; } = null!;

    public string Model { get; set; } = "";

    public string? VoiceSource { get; set; }

    public long SynthesisDurationMs { get; set; }

    public long? AudioDurationMs { get; set; }

    public double? RealTimeFactor { get; set; }

    public bool? UsedCuda { get; set; }

    public bool UsedFp16 { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
