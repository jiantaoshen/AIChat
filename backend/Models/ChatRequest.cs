// This file defines one idempotent browser chat turn. Conversation history is owned by the backend, not supplied by the client.
namespace AiAvatar.Backend.Models;

public sealed record ChatRequest(
    Guid? ConversationId,
    Guid TurnId,
    string Message);
