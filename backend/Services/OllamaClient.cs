// This file calls local Ollama with Qwen 4B, constrains output with JSON Schema, parses inference telemetry, and returns a validated avatar decision.
using System.Net.Http.Json;
using System.Text.Json;
using AiAvatar.Backend.Models;
using AiAvatar.Backend.Options;
using Microsoft.Extensions.Options;

namespace AiAvatar.Backend.Services;

public sealed class OllamaClient
{
    private const int MaxConversationMessages = 12;

    private static readonly object AvatarDecisionSchema = new
    {
        type = "object",
        properties = new
        {
            speech = new
            {
                type = "string",
                description = "The short natural-language reply spoken to the user."
            },
            emotion = new
            {
                type = "string",
                @enum = new[] { "neutral", "happy", "sad", "angry", "surprised", "confused" }
            },
            emotionIntensity = new
            {
                type = "number",
                minimum = 0.0,
                maximum = 1.0
            },
            gesture = new
            {
                type = "string",
                @enum = new[] { "none", "nod", "shake", "jump" }
            },
            gestureIntensity = new
            {
                type = "number",
                minimum = 0.0,
                maximum = 1.0
            }
        },
        required = new[]
        {
            "speech",
            "emotion",
            "emotionIntensity",
            "gesture",
            "gestureIntensity"
        },
        additionalProperties = false
    };

    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _ollama;
    private readonly CharacterOptions _character;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public OllamaClient(
        HttpClient httpClient,
        IOptions<OllamaOptions> ollama,
        IOptions<CharacterOptions> character)
    {
        _httpClient = httpClient;
        _ollama = ollama.Value;
        _character = character.Value;
    }

    public async Task<AvatarChatResponse> CreateDecisionAsync(
        IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        var sanitizedConversation = messages
            .Where(IsSupportedMessage)
            .TakeLast(MaxConversationMessages)
            .Select(message => new
            {
                role = message.Role.Trim().ToLowerInvariant(),
                content = message.Content.Trim()
            })
            .ToArray();

        if (sanitizedConversation.Length == 0)
        {
            throw new ArgumentException("At least one non-empty chat message is required.");
        }

        var payload = new
        {
            model = _ollama.Model,
            stream = false,
            keep_alive = _ollama.KeepAlive,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = AvatarSystemPrompt.Build(_character)
                }
            }
            .Concat(sanitizedConversation.Cast<object>()),
            format = AvatarDecisionSchema,
            options = new
            {
                temperature = _ollama.Temperature,
                num_ctx = _ollama.ContextLength,
                num_predict = _ollama.MaxOutputTokens,
                top_p = 0.9,
                repeat_penalty = 1.05
            }
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            BuildUri("api/chat"))
        {
            Content = JsonContent.Create(payload)
        };

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                BuildFriendlyOllamaError(response.StatusCode, responseBody));
        }

        using var envelope = JsonDocument.Parse(responseBody);
        var root = envelope.RootElement;

        var content = root
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        var telemetry = new ModelTelemetry(
            root.TryGetProperty("model", out var modelElement)
                ? modelElement.GetString() ?? _ollama.Model
                : _ollama.Model,
            ReadNanosecondsAsMilliseconds(root, "total_duration"),
            ReadNanosecondsAsMilliseconds(root, "load_duration"),
            ReadNullableInt(root, "prompt_eval_count"),
            ReadNullableInt(root, "eval_count"));

        if (string.IsNullOrWhiteSpace(content))
        {
            var fallback = AvatarMotionPolicy.Apply(
                AvatarDecisionValidator.Fallback("The local model returned an empty response."));

            return new AvatarChatResponse(fallback, telemetry);
        }

        try
        {
            var rawDecision = JsonSerializer.Deserialize<AvatarDecision>(
                content,
                _jsonOptions);

            var validated = AvatarDecisionValidator.Sanitize(rawDecision);
            var animated = AvatarMotionPolicy.Apply(validated);
            return new AvatarChatResponse(animated, telemetry);
        }
        catch (JsonException)
        {
            var fallback = AvatarMotionPolicy.Apply(
                AvatarDecisionValidator.Fallback(
                    "The local model answered, but its avatar-control JSON was invalid."));

            return new AvatarChatResponse(fallback, telemetry);
        }
    }

    public async Task<(bool Reachable, bool ModelInstalled, string Message)> GetStatusAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                BuildUri("api/tags"),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return (false, false, $"Ollama returned HTTP {(int)response.StatusCode}.");
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(body);

            var installed = document.RootElement
                .GetProperty("models")
                .EnumerateArray()
                .Select(item => item.TryGetProperty("name", out var name) ? name.GetString() : null)
                .Any(name => string.Equals(name, _ollama.Model, StringComparison.OrdinalIgnoreCase));

            return installed
                ? (true, true, $"Ollama is ready with {_ollama.Model}.")
                : (true, false, $"Ollama is running, but {_ollama.Model} is not installed.");
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return (false, false, $"Ollama is not reachable: {exception.Message}");
        }
    }

    private Uri BuildUri(string relativePath) =>
        new(new Uri(EnsureTrailingSlash(_ollama.BaseUrl)), relativePath);

    private static bool IsSupportedMessage(ChatMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Content) ||
            string.IsNullOrWhiteSpace(message.Role))
        {
            return false;
        }

        var role = message.Role.Trim().ToLowerInvariant();
        return role is "user" or "assistant";
    }

    private static string EnsureTrailingSlash(string baseUrl) =>
        baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/";

    private string BuildFriendlyOllamaError(
        System.Net.HttpStatusCode statusCode,
        string responseBody)
    {
        if (statusCode == System.Net.HttpStatusCode.NotFound)
        {
            return $"Ollama could not find model '{_ollama.Model}'. Run: ollama pull {_ollama.Model}";
        }

        return $"Ollama returned HTTP {(int)statusCode}: {responseBody}";
    }

    private static double? ReadNanosecondsAsMilliseconds(
        JsonElement root,
        string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element) ||
            !element.TryGetInt64(out var value))
        {
            return null;
        }

        return Math.Round(value / 1_000_000d, 1);
    }

    private static int? ReadNullableInt(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var element) && element.TryGetInt32(out var value)
            ? value
            : null;
}
