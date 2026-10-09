// This file defines expected application/domain failures so endpoints do not depend on transport- or framework-specific exception types.
namespace AiAvatar.Backend.Errors;

public abstract class DomainException : Exception
{
    protected DomainException(string message)
        : base(message)
    {
    }

    protected DomainException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

public sealed class ResourceNotFoundException(string message) : DomainException(message);

public sealed class SpeechSynthesisRejectedException(string message) : DomainException(message);

public abstract class LocalDependencyException : DomainException
{
    protected LocalDependencyException(
        string dependencyName,
        string publicMessage,
        string diagnosticMessage,
        Exception? innerException = null)
        : base(diagnosticMessage, innerException)
    {
        DependencyName = dependencyName;
        PublicMessage = publicMessage;
    }

    public string DependencyName { get; }

    public string PublicMessage { get; }
}

public sealed class LocalDependencyUnavailableException : LocalDependencyException
{
    public LocalDependencyUnavailableException(
        string dependencyName,
        string publicMessage,
        string diagnosticMessage,
        Exception? innerException = null)
        : base(dependencyName, publicMessage, diagnosticMessage, innerException)
    {
    }
}

public sealed class LocalDependencyTimeoutException : LocalDependencyException
{
    public LocalDependencyTimeoutException(
        string dependencyName,
        string publicMessage,
        string diagnosticMessage,
        Exception? innerException = null)
        : base(dependencyName, publicMessage, diagnosticMessage, innerException)
    {
    }
}
