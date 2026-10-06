// This file performs deterministic input validation before conversation data is persisted or sent to Ollama.
using AiAvatar.Backend.Models;

namespace AiAvatar.Backend.Services;

public static class ChatRequestValidator
{
    private const int MaxCharactersPerMessage = 4_000;

    public static string? Validate(ChatRequest? request)
    {
        if (request?.Messages is null || request.Messages.Count == 0)
        {
            return "At least one chat message is required.";
        }

        if (request.ConversationId == Guid.Empty)
        {
            return "ConversationId must be null or a non-empty GUID.";
        }


        foreach (var message in request.Messages)
        {
            if (message is null ||
                string.IsNullOrWhiteSpace(message.Role) ||
                message.Content is null)
            {
                return "Every message must contain a role and text content.";
            }

            var role = message.Role.Trim().ToLowerInvariant();
            if (role is not ("user" or "assistant"))
            {
                return $"Unsupported chat role: {message.Role}";
            }

            if (message.Content.Length > MaxCharactersPerMessage)
            {
                return $"A single message cannot exceed {MaxCharactersPerMessage:N0} characters in this MVP.";
            }
        }

        var latestMessage = request.Messages[^1];
        if (!string.Equals(
                latestMessage.Role.Trim(),
                "user",
                StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(latestMessage.Content))
        {
            return "The latest chat message must be a non-empty user message.";
        }

        return null;
    }
}
