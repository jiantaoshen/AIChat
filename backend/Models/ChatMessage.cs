// This file defines one user or assistant conversation message accepted by the local backend.
namespace AiAvatar.Backend.Models;

public sealed record ChatMessage(
    string Role,
    string Content);
