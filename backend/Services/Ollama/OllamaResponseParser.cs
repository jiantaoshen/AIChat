// This file parses a successful Ollama chat envelope into validated avatar decisions and normalized timing/token telemetry.
using System.Text.Json;
using AiAvatar.Backend.Models;
using AiAvatar.Backend.Options;
using AiAvatar.Backend.Services;
using Microsoft.Extensions.Options;

namespace AiAvatar.Backend.Services.Ollama;

public sealed class OllamaResponseParser
{
    private readonly OllamaOptions _ollama;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public OllamaResponseParser(IOptions<OllamaOptions> ollama)
    {
        _ollama = ollama.Value;
    }

    public OllamaDecisionResult Parse(string responseBody)
    {
        using var envelope = JsonDocument.Parse(responseBody);
        var root = envelope.RootElement;
        var content = root.GetProperty("message").GetProperty("content").GetString();
        var telemetry = ReadTelemetry(root);

        if (string.IsNullOrWhiteSpace(content))
        {
            return CreateFallback(
                "The local model returned an empty response.",
                telemetry);
        }

        try
        {
            var rawDecision = JsonSerializer.Deserialize<AvatarDecision>(content, _jsonOptions);
            var validated = AvatarDecisionValidator.Sanitize(rawDecision);
            var animated = AvatarMotionPolicy.Apply(validated);
            return new OllamaDecisionResult(animated, telemetry);
        }
        catch (JsonException)
        {
            return CreateFallback(
                "The local model answered, but its avatar-control JSON was invalid.",
                telemetry);
        }
    }

    private OllamaDecisionResult CreateFallback(string message, ModelTelemetry telemetry)
    {
        var fallback = AvatarMotionPolicy.Apply(AvatarDecisionValidator.Fallback(message));
        return new OllamaDecisionResult(fallback, telemetry);
    }

    private ModelTelemetry ReadTelemetry(JsonElement root) =>
        new(
            root.TryGetProperty("model", out var modelElement)
                ? modelElement.GetString() ?? _ollama.Model
                : _ollama.Model,
            ReadNanosecondsAsMilliseconds(root, "total_duration"),
            ReadNanosecondsAsMilliseconds(root, "load_duration"),
            ReadNullableInt(root, "prompt_eval_count"),
            ReadNullableInt(root, "eval_count"));

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
