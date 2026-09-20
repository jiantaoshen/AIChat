// This file performs deterministic input validation before conversation data is sent to Ollama.
using AiAvatar.Backend.Models;

namespace AiAvatar.Backend.Services;

public static class ChatRequestValidator
{
    private const int MaxMessages = 40;
    private const int MaxCharactersPerMessage = 4_000;

    public static string? Validate(ChatRequest? request)
    {
        if (request?.Messages is null || request.Messages.Count == 0)
        {
            return "At least one chat message is required.";
        }

        if (request.Messages.Count > MaxMessages)
        {
            return $"This MVP accepts at most {MaxMessages} messages per request.";
        }

        foreach (var message in request.Messages)
        {
            if (message is null ||
                string.IsNullOrWhiteSpace(message.Role) ||
                message.Content is null)
            {
                return "Every message must contain a role and text content.";
            }

            if (message.Content.Length > MaxCharactersPerMessage)
            {
                return $"A single message cannot exceed {MaxCharactersPerMessage:N0} characters in this MVP.";
            }
        }

        return null;
    }
}
