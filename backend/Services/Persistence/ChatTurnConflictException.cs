// This domain exception means an idempotency key was reused for a different logical chat turn.
using AiAvatar.Backend.Errors;

namespace AiAvatar.Backend.Services.Persistence;

public sealed class ChatTurnConflictException(string message) : DomainException(message);
