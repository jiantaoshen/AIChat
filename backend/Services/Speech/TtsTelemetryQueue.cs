using System.Threading.Channels;
using AiAvatar.Backend.Models;

namespace AiAvatar.Backend.Services.Speech;

public sealed record TtsTelemetryWorkItem(
    Guid MessageId,
    TtsTelemetryRecord Telemetry);

public sealed class TtsTelemetryQueue : ITtsTelemetrySink
{
    private const int DefaultCapacity = 128;
    private readonly Channel<TtsTelemetryWorkItem> _channel;

    public TtsTelemetryQueue()
        : this(DefaultCapacity)
    {
    }

    public TtsTelemetryQueue(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                "TTS telemetry queue capacity must be greater than zero.");
        }

        _channel = Channel.CreateBounded<TtsTelemetryWorkItem>(
            new BoundedChannelOptions(capacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait,
            });
    }

    public bool TryRecord(Guid messageId, TtsTelemetryRecord telemetry) =>
        _channel.Writer.TryWrite(new TtsTelemetryWorkItem(messageId, telemetry));

    public IAsyncEnumerable<TtsTelemetryWorkItem> ReadAllAsync(
        CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
