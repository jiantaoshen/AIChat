// This file is the thin Ollama HTTP client: it sends prepared requests, translates transport failures, and exposes model readiness checks.
using System.Text.Json;
using AiAvatar.Backend.Errors;
using AiAvatar.Backend.Models;
using AiAvatar.Backend.Options;
using Microsoft.Extensions.Options;

namespace AiAvatar.Backend.Services.Ollama;

public sealed class OllamaClient : IChatDecisionGenerator
{
    private readonly HttpClient _httpClient;
    private readonly OllamaRequestFactory _requestFactory;
    private readonly OllamaResponseParser _responseParser;
    private readonly OllamaOptions _ollama;

    public OllamaClient(
        HttpClient httpClient,
        OllamaRequestFactory requestFactory,
        OllamaResponseParser responseParser,
        IOptions<OllamaOptions> ollama)
    {
        _httpClient = httpClient;
        _requestFactory = requestFactory;
        _responseParser = responseParser;
        _ollama = ollama.Value;
    }

    public async Task<OllamaDecisionResult> CreateDecisionAsync(
        IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = _requestFactory.CreateChatRequest(messages);
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new LocalDependencyUnavailableException(
                    "Ollama",
                    "Local Ollama could not complete the model request. Check that Ollama is running and the configured model is installed.",
                    BuildFriendlyOllamaError(response.StatusCode, responseBody));
            }

            return _responseParser.Parse(responseBody);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new LocalDependencyTimeoutException(
                "Ollama",
                "Local Qwen inference timed out. Try again after the local model is ready.",
                "The Ollama HTTP request exceeded the configured client timeout.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new LocalDependencyUnavailableException(
                "Ollama",
                "Local Ollama could not complete the model request. Check that Ollama is running and the configured model is installed.",
                $"The Ollama HTTP request failed: {exception.Message}",
                exception);
        }
    }

    public async Task<(bool Reachable, bool ModelInstalled, string Message)> GetStatusAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                _requestFactory.BuildUri("api/tags"),
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
}
