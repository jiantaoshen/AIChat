// This handler is the single HTTP error boundary: expected domain failures become safe ProblemDetails, while unexpected failures are logged with their full stack trace.
using AiAvatar.Backend.Errors;
using AiAvatar.Backend.Services.Persistence;
using Microsoft.AspNetCore.Diagnostics;

namespace AiAvatar.Backend.Infrastructure;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    private const int ClientClosedRequestStatusCode = 499;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException &&
            httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug(
                "Request {Method} {Path} was cancelled by the client. TraceId: {TraceId}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                httpContext.TraceIdentifier);

            httpContext.Response.StatusCode = ClientClosedRequestStatusCode;
            return true;
        }

        var problem = Map(exception);

        if (problem.IsUnexpected)
        {
            logger.LogError(
                exception,
                "Unhandled exception for {Method} {Path}. TraceId: {TraceId}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                httpContext.TraceIdentifier);
        }
        else if (exception is LocalDependencyException dependencyException)
        {
            logger.LogWarning(
                exception,
                "Local dependency {DependencyName} failed for {Method} {Path}. TraceId: {TraceId}",
                dependencyException.DependencyName,
                httpContext.Request.Method,
                httpContext.Request.Path,
                httpContext.TraceIdentifier);
        }
        else
        {
            logger.LogInformation(
                "Request {Method} {Path} ended with expected {ExceptionType}. TraceId: {TraceId}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                exception.GetType().Name,
                httpContext.TraceIdentifier);
        }

        await Results.Problem(
                statusCode: problem.StatusCode,
                title: problem.Title,
                detail: problem.Detail)
            .ExecuteAsync(httpContext);

        return true;
    }

    private static ProblemMapping Map(Exception exception) => exception switch
    {
        ChatTurnConflictException => new(
            StatusCodes.Status409Conflict,
            "Chat turn conflict",
            exception.Message,
            IsUnexpected: false),

        ResourceNotFoundException => new(
            StatusCodes.Status404NotFound,
            "Resource not found",
            exception.Message,
            IsUnexpected: false),

        LocalDependencyTimeoutException dependency => new(
            StatusCodes.Status504GatewayTimeout,
            $"{dependency.DependencyName} request timed out",
            dependency.PublicMessage,
            IsUnexpected: false),

        LocalDependencyUnavailableException dependency => new(
            StatusCodes.Status503ServiceUnavailable,
            $"{dependency.DependencyName} request failed",
            dependency.PublicMessage,
            IsUnexpected: false),

        _ => new(
            StatusCodes.Status500InternalServerError,
            "Unexpected backend error",
            "An unexpected backend error occurred. Check the server logs and use the response traceId to correlate the request.",
            IsUnexpected: true),
    };

    private sealed record ProblemMapping(
        int StatusCode,
        string Title,
        string Detail,
        bool IsUnexpected);
}
