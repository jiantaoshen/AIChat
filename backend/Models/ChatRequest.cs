// This file defines the browser request body containing the active conversation identifier and recent message context sent to the local model.
namespace AiAvatar.Backend.Models;

public sealed record ChatRequest(
    Guid? ConversationId,
    IReadOnlyList<ChatMessage> Messages);
