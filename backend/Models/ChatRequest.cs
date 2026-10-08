// This file defines the browser request body. The client identifies the conversation and submits only the new user message; persisted history is owned by the backend.
namespace AiAvatar.Backend.Models;

public sealed record ChatRequest(
    Guid? ConversationId,
    string Message);
