// This file validates one client chat turn before backend-owned conversation history is read or changed.
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

        if (request.TurnId == Guid.Empty)
        {
            return "TurnId must be a non-empty GUID.";
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return "Message must be non-empty.";
        }

        if (request.Message.Length > MaxCharactersPerMessage)
        {
            return $"Message cannot exceed {MaxCharactersPerMessage:N0} characters in this MVP.";
        }

        return null;
    }
}
