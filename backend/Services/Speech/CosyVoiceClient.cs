// This file forwards validated avatar speech requests to the long-running local CosyVoice3 service and returns WAV audio bytes to the ASP.NET Core API.
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using AiAvatar.Backend.Models;
using AiAvatar.Backend.Options;
using Microsoft.Extensions.Options;

namespace AiAvatar.Backend.Services.Speech;

public sealed class CosyVoiceClient
{
    private readonly HttpClient _httpClient;
    private readonly CosyVoiceOptions _options;
    private readonly ILogger<CosyVoiceClient> _logger;

    public CosyVoiceClient(
        HttpClient httpClient,
        IOptions<CosyVoiceOptions> options,
        ILogger<CosyVoiceClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return false;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));

        try
        {
            using var response = await _httpClient.GetAsync("/health", timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);

            return document.RootElement.TryGetProperty("ready", out var ready)
                && ready.ValueKind == JsonValueKind.True;
        }
        catch
        {
            return false;
        }
    }

    public async Task<byte[]> SynthesizeAsync(
        SpeechSynthesisRequest request,
        CancellationToken cancellationToken)
    {
        await WaitUntilReadyAsync(cancellationToken);

        var payload = TtsSpeechPolicy.CreatePayload(request);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(15, _options.SynthesisTimeoutSeconds)));

        var stopwatch = Stopwatch.StartNew();
        using var response = await _httpClient.PostAsJsonAsync(
            "/synthesize",
            payload,
            timeout.Token);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            throw new HttpRequestException(
                $"CosyVoice3 returned HTTP {(int)response.StatusCode}: {body.Trim()}");
        }

        var audio = await response.Content.ReadAsByteArrayAsync(timeout.Token);
        stopwatch.Stop();

        if (audio.Length == 0)
        {
            throw new HttpRequestException("CosyVoice3 returned an empty audio response.");
        }

        _logger.LogInformation(
            "Local TTS completed in {DurationMs} ms and returned {AudioBytes} bytes.",
            stopwatch.ElapsedMilliseconds,
            audio.Length);

        return audio;
    }

    private async Task WaitUntilReadyAsync(CancellationToken cancellationToken)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(10, _options.StartupTimeoutSeconds));

        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await IsReadyAsync(cancellationToken))
            {
                return;
            }

            await Task.Delay(750, cancellationToken);
        }

        throw new HttpRequestException(
            "Local CosyVoice3 service is not ready. Run SETUP_COSYVOICE_WINDOWS.cmd and check VOICE_TTS_SETUP_WINDOWS.md.");
    }
}
