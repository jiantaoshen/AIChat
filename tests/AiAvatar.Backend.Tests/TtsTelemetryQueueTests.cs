using AiAvatar.Backend.Models;
using AiAvatar.Backend.Services.Speech;

namespace AiAvatar.Backend.Tests;

public sealed class TtsTelemetryQueueTests
{
    [Fact]
    public async Task TryRecord_IsBoundedAndNeverWaitsForDatabasePersistence()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var queue = new TtsTelemetryQueue(capacity: 1);
        var firstMessageId = Guid.NewGuid();
        var telemetry = new TtsTelemetryRecord(
            SynthesisDurationMs: 25,
            Model: "test-tts",
            VoiceSource: "reference.wav",
            UsedCuda: false,
            UsedFp16: false);

        Assert.True(queue.TryRecord(firstMessageId, telemetry));
        Assert.False(queue.TryRecord(Guid.NewGuid(), telemetry));

        await using var reader = queue.ReadAllAsync(cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        Assert.True(await reader.MoveNextAsync());
        Assert.Equal(firstMessageId, reader.Current.MessageId);
        Assert.Equal(telemetry, reader.Current.Telemetry);
    }
}
