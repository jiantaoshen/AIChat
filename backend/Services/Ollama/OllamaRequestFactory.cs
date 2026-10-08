// This file serializes an already-bounded server-owned conversation into an Ollama request together with the system prompt and inference options.
using System.Net.Http.Json;
using AiAvatar.Backend.Models;
using AiAvatar.Backend.Options;
using AiAvatar.Backend.Services;
using Microsoft.Extensions.Options;

namespace AiAvatar.Backend.Services.Ollama;

public sealed class OllamaRequestFactory
{
    private readonly OllamaOptions _ollama;
    private readonly CharacterOptions _character;

    public OllamaRequestFactory(
        IOptions<OllamaOptions> ollama,
        IOptions<CharacterOptions> character)
    {
        _ollama = ollama.Value;
        _character = character.Value;
    }

    public HttpRequestMessage CreateChatRequest(IReadOnlyList<ChatMessage> messages)
    {
        var conversation = messages
            .Where(IsSupportedMessage)
            .Select(message => new
            {
                role = message.Role.Trim().ToLowerInvariant(),
                content = message.Content.Trim()
            })
            .ToArray();

        if (conversation.Length == 0)
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
            }.Concat(conversation.Cast<object>()),
            format = AvatarDecisionSchema.Value,
            options = new
            {
                temperature = _ollama.Temperature,
                num_ctx = _ollama.ContextLength,
                num_predict = _ollama.MaxOutputTokens,
                top_p = 0.9,
                repeat_penalty = 1.05
            }
        };

        return new HttpRequestMessage(HttpMethod.Post, BuildUri("api/chat"))
        {
            Content = JsonContent.Create(payload)
        };
    }

    public Uri BuildUri(string relativePath) =>
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
}
