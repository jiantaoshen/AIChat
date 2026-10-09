using AiAvatar.Backend.Services.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AiAvatar.Backend.Services.Speech;

// Persists TTS observability outside the synthesis response path. Each item gets
// a fresh scope/DbContext so the request-scoped SpeechRepository is never captured
// by background work.
public sealed class TtsTelemetryBackgroundService(
    TtsTelemetryQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<TtsTelemetryBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var item in queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var repository = scope.ServiceProvider
                        .GetRequiredService<ISpeechRepository>();

                    await repository.SaveTtsTelemetryAsync(
                        item.MessageId,
                        item.Telemetry,
                        stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    logger.LogWarning(
                        exception,
                        "TTS telemetry persistence failed for assistant message {MessageId}; audio synthesis remains successful.",
                        item.MessageId);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
    }
}
