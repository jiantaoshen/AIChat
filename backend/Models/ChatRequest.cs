// This file defines the browser request body containing the recent conversation history sent to the local model.
namespace AiAvatar.Backend.Models;

public sealed record ChatRequest(
    IReadOnlyList<ChatMessage> Messages);
