// This file defines one persisted conversation plus its ordered messages for restoring a browser chat session.
namespace AiAvatar.Backend.Models;

public sealed record ConversationSnapshot(
    Guid Id,
    string Title,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<ChatMessage> Messages);
