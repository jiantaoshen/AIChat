// This file validates one client chat turn before backend-owned conversation history is read or changed.
using AiAvatar.Backend.Models;
using AiAvatar.Backend.Services.Ollama;

namespace AiAvatar.Backend.Services;

public static class ChatRequestValidator
{
    private const int MaxCharactersPerMessage = 4_000;

    // Retained for focused validation tests and callers that only need request-shape checks.
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

    public static string? Validate(
        ChatRequest? request,
        ConversationContextPolicy contextPolicy)
    {
        var structuralError = Validate(request);
        if (structuralError is not null)
        {
            return structuralError;
        }

        if (!contextPolicy.CanFitCurrentUserMessage(request!.Message))
        {
            return "Message cannot fit within the configured Ollama context window after " +
                   "reserving the system prompt, output tokens, and context safety margin.";
        }

        return null;
    }
}
