using System.Net;
using System.Text;
using System.Text.Json;
using AiAvatar.Backend.Errors;
using AiAvatar.Backend.Models;
using AiAvatar.Backend.Options;
using AiAvatar.Backend.Services.Persistence;
using AiAvatar.Backend.Services.Speech;
using Microsoft.Extensions.Logging.Abstractions;

namespace AiAvatar.Backend.Tests;

public sealed class SpeechSynthesisServiceTests
{
    [Fact]
    public void SpeechSynthesisRequest_ContainsOnlyPersistedMessageId()
    {
        var properties = typeof(SpeechSynthesisRequest).GetProperties();

        var property = Assert.Single(properties);
        Assert.Equal(nameof(SpeechSynthesisRequest.MessageId), property.Name);
        Assert.Equal(typeof(Guid), property.PropertyType);
    }

    [Fact]
    public async Task SynthesizeAsync_UsesPersistedAssistantSemanticsAndQueuesTelemetry()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var messageId = Guid.NewGuid();
        var repository = new FakeSpeechRepository(
            new AssistantSpeechSource(
                "persisted authoritative speech",
                "en",
                "sad",
                1.0));
        var telemetrySink = new CapturingTelemetrySink();
        var handler = new CapturingCosyVoiceHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:8188"),
        };
        var options = CreateOptions();
        var cosyVoice = new CosyVoiceClient(
            httpClient,
            options,
            NullLogger<CosyVoiceClient>.Instance);
        var service = new SpeechSynthesisService(
            cosyVoice,
            repository,
            telemetrySink,
            options,
            NullLogger<SpeechSynthesisService>.Instance);

        var result = await service.SynthesizeAsync(messageId, cancellationToken);

        Assert.Equal(new byte[] { 1, 2, 3 }, result.Audio);
        Assert.NotNull(handler.CapturedPayload);
        Assert.Equal("persisted authoritative speech", handler.CapturedPayload.Text);
        Assert.InRange(Math.Abs(handler.CapturedPayload.Speed - 0.91), 0, 1e-10);
        Assert.Equal(messageId, telemetrySink.MessageId);
        Assert.NotNull(telemetrySink.Telemetry);
        Assert.Equal(result.Model, telemetrySink.Telemetry.Model);
        Assert.Equal(result.SynthesisDurationMs, telemetrySink.Telemetry.SynthesisDurationMs);
        Assert.Equal(0, repository.TelemetrySaveCalls);
    }

    [Fact]
    public async Task SynthesizeAsync_ReturnsSuccessfulAudioWhenTelemetryQueueCannotAcceptRecord()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var messageId = Guid.NewGuid();
        var repository = new FakeSpeechRepository(
            new AssistantSpeechSource(
                "audio must win over observability",
                "en",
                "neutral",
                0.2));
        var telemetrySink = new CapturingTelemetrySink(accept: false);
        var handler = new CapturingCosyVoiceHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:8188"),
        };
        var options = CreateOptions();
        var cosyVoice = new CosyVoiceClient(
            httpClient,
            options,
            NullLogger<CosyVoiceClient>.Instance);
        var service = new SpeechSynthesisService(
            cosyVoice,
            repository,
            telemetrySink,
            options,
            NullLogger<SpeechSynthesisService>.Instance);

        var result = await service.SynthesizeAsync(messageId, cancellationToken);

        Assert.Equal(new byte[] { 1, 2, 3 }, result.Audio);
        Assert.Equal(1, handler.SynthesizeCalls);
        Assert.Equal(1, telemetrySink.RecordCalls);
        Assert.Equal(0, repository.TelemetrySaveCalls);
    }

    [Fact]
    public async Task SynthesizeAsync_RejectsPersistedUnsupportedLanguageBeforeCosyVoiceCall()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var messageId = Guid.NewGuid();
        var repository = new FakeSpeechRepository(
            new AssistantSpeechSource(
                "det här ska förbli text",
                "sv",
                "neutral",
                0.2));
        var telemetrySink = new CapturingTelemetrySink();
        var handler = new CapturingCosyVoiceHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:8188"),
        };
        var options = CreateOptions();
        var cosyVoice = new CosyVoiceClient(
            httpClient,
            options,
            NullLogger<CosyVoiceClient>.Instance);
        var service = new SpeechSynthesisService(
            cosyVoice,
            repository,
            telemetrySink,
            options,
            NullLogger<SpeechSynthesisService>.Instance);

        var exception = await Assert.ThrowsAsync<SpeechSynthesisRejectedException>(
            () => service.SynthesizeAsync(messageId, cancellationToken));

        Assert.Contains("language", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, handler.SynthesizeCalls);
        Assert.Equal(0, telemetrySink.RecordCalls);
        Assert.Equal(0, repository.TelemetrySaveCalls);
    }

    private static Microsoft.Extensions.Options.IOptions<CosyVoiceOptions> CreateOptions() =>
        Microsoft.Extensions.Options.Options.Create(
            new CosyVoiceOptions
            {
                Enabled = true,
                ModelPath = "test-model",
                StartupTimeoutSeconds = 10,
                SynthesisTimeoutSeconds = 15,
                MaxTextCharacters = 800,
            });

    private sealed class CapturingCosyVoiceHandler : HttpMessageHandler
    {
        public CosyVoiceSynthesisPayload? CapturedPayload { get; private set; }

        public int SynthesizeCalls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath == "/health")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {"service":"ai-avatar-cosyvoice","contractVersion":1,"ready":true,"model":"test-model","voiceSource":"test.wav","cudaAvailable":false}
                        """,
                        Encoding.UTF8,
                        "application/json"),
                };
            }

            if (request.RequestUri?.AbsolutePath == "/synthesize")
            {
                SynthesizeCalls += 1;
                var json = await request.Content!.ReadAsStringAsync(cancellationToken);
                CapturedPayload = JsonSerializer.Deserialize<CosyVoiceSynthesisPayload>(
                    json,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([1, 2, 3]),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }

    private sealed class FakeSpeechRepository(AssistantSpeechSource source) : ISpeechRepository
    {
        public int TelemetrySaveCalls { get; private set; }

        public Task<AssistantSpeechSource?> GetAssistantSpeechSourceAsync(
            Guid messageId,
            CancellationToken cancellationToken) =>
            Task.FromResult<AssistantSpeechSource?>(source);

        public Task SaveTtsTelemetryAsync(
            Guid messageId,
            TtsTelemetryRecord telemetry,
            CancellationToken cancellationToken)
        {
            TelemetrySaveCalls += 1;
            throw new InvalidOperationException(
                "Request-path speech synthesis must not write telemetry directly.");
        }
    }

    private sealed class CapturingTelemetrySink(bool accept = true) : ITtsTelemetrySink
    {
        public int RecordCalls { get; private set; }

        public Guid? MessageId { get; private set; }

        public TtsTelemetryRecord? Telemetry { get; private set; }

        public bool TryRecord(Guid messageId, TtsTelemetryRecord telemetry)
        {
            RecordCalls += 1;
            MessageId = messageId;
            Telemetry = telemetry;
            return accept;
        }
    }
}
