// This file forwards validated avatar speech requests to the long-running local CosyVoice3 service and returns WAV audio plus runtime telemetry.
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
        var status = await ReadRuntimeStatusAsync(cancellationToken);
        return status.Ready;
    }

    public async Task<SpeechSynthesisResult> SynthesizeAsync(
        SpeechSynthesisRequest request,
        CancellationToken cancellationToken)
    {
        var runtimeStatus = await WaitUntilReadyAsync(cancellationToken);
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

        var model = ReadHeader(response, "X-CosyVoice-Model")
            ?? runtimeStatus.Model
            ?? Path.GetFileName(_options.ModelPath.TrimEnd('/', '\\'))
            ?? "CosyVoice3";

        var voiceSource = ReadHeader(response, "X-CosyVoice-Voice-Source")
            ?? runtimeStatus.VoiceSource;

        _logger.LogInformation(
            "Local TTS completed in {DurationMs} ms and returned {AudioBytes} bytes.",
            stopwatch.ElapsedMilliseconds,
            audio.Length);

        return new SpeechSynthesisResult(
            audio,
            stopwatch.ElapsedMilliseconds,
            model,
            voiceSource,
            runtimeStatus.CudaAvailable,
            UsedFp16: false);
    }

    private async Task<CosyVoiceRuntimeStatus> WaitUntilReadyAsync(
        CancellationToken cancellationToken)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(
            Math.Max(10, _options.StartupTimeoutSeconds));

        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var status = await ReadRuntimeStatusAsync(cancellationToken);
            if (status.Ready)
            {
                return status;
            }

            await Task.Delay(750, cancellationToken);
        }

        throw new HttpRequestException(
            "Local CosyVoice3 service is not ready. See WINDOWS_SETUP.md for installation and VOICE_TTS_SETUP_WINDOWS.md for TTS troubleshooting.");
    }

    private async Task<CosyVoiceRuntimeStatus> ReadRuntimeStatusAsync(
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return CosyVoiceRuntimeStatus.Unavailable;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));

        try
        {
            using var response = await _httpClient.GetAsync("/health", timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return CosyVoiceRuntimeStatus.Unavailable;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: timeout.Token);

            var root = document.RootElement;
            var ready = root.TryGetProperty("ready", out var readyElement)
                && readyElement.ValueKind == JsonValueKind.True;

            var model = ReadNullableString(root, "model");
            var voiceSource = ReadNullableString(root, "voiceSource");
            var cudaAvailable = ReadNullableBoolean(root, "cudaAvailable");

            return new CosyVoiceRuntimeStatus(
                ready,
                model,
                voiceSource,
                cudaAvailable);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return CosyVoiceRuntimeStatus.Unavailable;
        }
    }

    private static string? ReadHeader(HttpResponseMessage response, string name)
    {
        return response.Headers.TryGetValues(name, out var values)
            ? values.FirstOrDefault()
            : null;
    }

    private static string? ReadNullableString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element) ||
            element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return element.GetString();
    }

    private static bool? ReadNullableBoolean(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            return null;
        }

        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private sealed record CosyVoiceRuntimeStatus(
        bool Ready,
        string? Model,
        string? VoiceSource,
        bool? CudaAvailable)
    {
        public static CosyVoiceRuntimeStatus Unavailable { get; } =
            new(false, null, null, null);
    }
}
