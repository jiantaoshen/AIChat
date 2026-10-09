using System.Net;
using System.Text;
using AiAvatar.Backend.Errors;
using AiAvatar.Backend.Models;
using AiAvatar.Backend.Options;
using AiAvatar.Backend.Services.Speech;
using Microsoft.Extensions.Logging.Abstractions;

namespace AiAvatar.Backend.Tests;

public sealed class CosyVoiceClientServiceDiscoveryTests
{
    [Fact]
    public async Task SynthesizeAsync_RejectsUnidentifiedHealthServiceBeforePostingAudioRequest()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new UnidentifiedServiceHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:8188"),
        };
        var options = Microsoft.Extensions.Options.Options.Create(
            new CosyVoiceOptions
            {
                Enabled = true,
                ModelPath = "expected-model",
                StartupTimeoutSeconds = 10,
                SynthesisTimeoutSeconds = 15,
            });
        var client = new CosyVoiceClient(
            httpClient,
            options,
            NullLogger<CosyVoiceClient>.Instance);

        var exception = await Assert.ThrowsAsync<LocalDependencyUnavailableException>(
            () => client.SynthesizeAsync(
                new SpeechSynthesisInput("hello", "en", "neutral", 0.2),
                cancellationToken));

        Assert.Contains("service identity mismatch", exception.Message);
        Assert.Equal(0, handler.SynthesizeCalls);
    }

    private sealed class UnidentifiedServiceHandler : HttpMessageHandler
    {
        public int SynthesizeCalls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath == "/health")
            {
                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            """
                            {"ready":true,"model":"expected-model","voiceSource":"custom-reference","cudaAvailable":false}
                            """,
                            Encoding.UTF8,
                            "application/json"),
                    });
            }

            if (request.RequestUri?.AbsolutePath == "/synthesize")
            {
                SynthesizeCalls += 1;
                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent([1, 2, 3]),
                    });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
