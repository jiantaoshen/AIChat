// This file defines the lightweight SQLite-backed conversation summary shown in the browser archive sidebar.
namespace AiAvatar.Backend.Models;

public sealed record ConversationArchiveItem(
    Guid Id,
    string Title,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    int MessageCount);
