using AiAvatar.Backend.Models;
using AiAvatar.Backend.Services.Persistence;
using AiAvatar.Backend.Services.Speech;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AiAvatar.Backend.Tests;

public sealed class TtsTelemetryBackgroundServiceTests
{
    [Fact]
    public async Task BackgroundPersistenceFailure_IsLoggedAndDoesNotStopLaterTelemetry()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FailingOnceSpeechRepository();
        var services = new ServiceCollection();
        services.AddScoped<ISpeechRepository>(_ => repository);
        using var serviceProvider = services.BuildServiceProvider();

        var queue = new TtsTelemetryQueue(capacity: 4);
        var worker = new TtsTelemetryBackgroundService(
            queue,
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<TtsTelemetryBackgroundService>.Instance);

        await worker.StartAsync(cancellationToken);
        try
        {
            var telemetry = new TtsTelemetryRecord(
                SynthesisDurationMs: 25,
                Model: "test-tts",
                VoiceSource: "reference.wav",
                UsedCuda: false,
                UsedFp16: false);

            Assert.True(queue.TryRecord(Guid.NewGuid(), telemetry));
            Assert.True(queue.TryRecord(Guid.NewGuid(), telemetry));

            await repository.SecondSuccessfulSave.Task.WaitAsync(
                TimeSpan.FromSeconds(2),
                cancellationToken);

            Assert.Equal(2, repository.SaveCalls);
        }
        finally
        {
            await worker.StopAsync(cancellationToken);
        }
    }

    private sealed class FailingOnceSpeechRepository : ISpeechRepository
    {
        public int SaveCalls { get; private set; }

        public TaskCompletionSource<bool> SecondSuccessfulSave { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<AssistantSpeechSource?> GetAssistantSpeechSourceAsync(
            Guid messageId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SaveTtsTelemetryAsync(
            Guid messageId,
            TtsTelemetryRecord telemetry,
            CancellationToken cancellationToken)
        {
            SaveCalls += 1;
            if (SaveCalls == 1)
            {
                throw new InvalidOperationException("forced telemetry DB failure");
            }

            SecondSuccessfulSave.TrySetResult(true);
            return Task.CompletedTask;
        }
    }
}
