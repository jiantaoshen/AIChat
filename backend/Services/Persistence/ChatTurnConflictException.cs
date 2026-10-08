// This exception indicates reuse of an idempotency key for a different logical chat turn.
namespace AiAvatar.Backend.Services.Persistence;

public sealed class ChatTurnConflictException(string message) : Exception(message)
{
}
