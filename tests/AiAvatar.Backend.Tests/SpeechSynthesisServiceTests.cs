using System.Net;
using System.Text;
using System.Text.Json;
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
    public async Task SynthesizeAsync_UsesPersistedAssistantSemanticsForCosyVoicePayload()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var messageId = Guid.NewGuid();
        var repository = new FakeSpeechRepository(
            new AssistantSpeechSource(
                "persisted authoritative speech",
                "sad",
                1.0));
        var handler = new CapturingCosyVoiceHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:8188"),
        };
        var options = Microsoft.Extensions.Options.Options.Create(
            new CosyVoiceOptions
            {
                Enabled = true,
                StartupTimeoutSeconds = 10,
                SynthesisTimeoutSeconds = 15,
                MaxTextCharacters = 800,
            });
        var cosyVoice = new CosyVoiceClient(
            httpClient,
            options,
            NullLogger<CosyVoiceClient>.Instance);
        var service = new SpeechSynthesisService(cosyVoice, repository, options);

        var result = await service.SynthesizeAsync(messageId, cancellationToken);

        Assert.Equal(new byte[] { 1, 2, 3 }, result.Audio);
        Assert.NotNull(handler.CapturedPayload);
        Assert.Equal("persisted authoritative speech", handler.CapturedPayload.Text);
        Assert.InRange(Math.Abs(handler.CapturedPayload.Speed - 0.91), 0, 1e-10);
        Assert.Equal(messageId, repository.SavedTelemetryMessageId);
    }

    private sealed class CapturingCosyVoiceHandler : HttpMessageHandler
    {
        public CosyVoiceSynthesisPayload? CapturedPayload { get; private set; }

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
                        {"ready":true,"model":"test-model","voiceSource":"test.wav","cudaAvailable":false}
                        """,
                        Encoding.UTF8,
                        "application/json"),
                };
            }

            if (request.RequestUri?.AbsolutePath == "/synthesize")
            {
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
        public Guid? SavedTelemetryMessageId { get; private set; }

        public Task<AssistantSpeechSource?> GetAssistantSpeechSourceAsync(
            Guid messageId,
            CancellationToken cancellationToken) =>
            Task.FromResult<AssistantSpeechSource?>(source);

        public Task SaveTtsTelemetryAsync(
            Guid messageId,
            SpeechSynthesisResult result,
            CancellationToken cancellationToken)
        {
            SavedTelemetryMessageId = messageId;
            return Task.CompletedTask;
        }
    }
}
