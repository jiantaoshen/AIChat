// This file wires the ASP.NET Core 10 API, CORS, local Ollama client, health endpoint, request validation, and chat endpoint.
using AiAvatar.Backend.Models;
using AiAvatar.Backend.Options;
using AiAvatar.Backend.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5191");

builder.Services.Configure<OllamaOptions>(
    builder.Configuration.GetSection(OllamaOptions.SectionName));

builder.Services.Configure<CharacterOptions>(
    builder.Configuration.GetSection(CharacterOptions.SectionName));

builder.Services.AddHttpClient<OllamaClient>(client =>
{
    // CPU inference can take noticeably longer on the first request while Ollama loads the model.
    client.Timeout = TimeSpan.FromSeconds(120);
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("NextJsDevelopment", policy =>
    {
        policy
            .WithOrigins("http://localhost:3000", "http://127.0.0.1:3000")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("NextJsDevelopment");

app.MapGet("/health", async (
    OllamaClient ollama,
    CancellationToken cancellationToken) =>
{
    var status = await ollama.GetStatusAsync(cancellationToken);

    return Results.Ok(new
    {
        backend = "ok",
        ollamaReachable = status.Reachable,
        modelInstalled = status.ModelInstalled,
        message = status.Message
    });
});

app.MapPost("/api/chat", async (
    ChatRequest request,
    OllamaClient ollama,
    CancellationToken cancellationToken) =>
{
    var validationError = ChatRequestValidator.Validate(request);
    if (validationError is not null)
    {
        return Results.BadRequest(new { error = validationError });
    }

    try
    {
        var response = await ollama.CreateDecisionAsync(
            request.Messages,
            cancellationToken);

        return Results.Ok(response);
    }
    catch (HttpRequestException exception)
    {
        return Results.Problem(
            detail: exception.Message,
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Local Ollama request failed");
    }
    catch (TaskCanceledException exception)
    {
        return Results.Problem(
            detail: $"Local model request timed out: {exception.Message}",
            statusCode: StatusCodes.Status504GatewayTimeout,
            title: "Qwen inference timed out");
    }
    catch (Exception exception)
    {
        return Results.Problem(
            detail: exception.Message,
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Unexpected backend error");
    }
});

app.Run();
