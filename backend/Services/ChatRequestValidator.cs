// This file performs deterministic input validation before conversation data is persisted or sent to Ollama.
using AiAvatar.Backend.Models;

namespace AiAvatar.Backend.Services;

public static class ChatRequestValidator
{
    private const int MaxCharactersPerMessage = 4_000;

    public static string? Validate(ChatRequest? request)
    {
        if (request is null)
        {
            return "A chat request is required.";
        }

        if (request.ConversationId == Guid.Empty)
        {
            return "ConversationId must be null or a non-empty GUID.";
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return "Message must be a non-empty user message.";
        }

        if (request.Message.Length > MaxCharactersPerMessage)
        {
            return $"A single message cannot exceed {MaxCharactersPerMessage:N0} characters in this MVP.";
        }

        return null;
    }
}
