// This file wires the ASP.NET Core 10 API, CORS, local Ollama/Qwen chat, CosyVoice3 avatar TTS, health checks, and request validation.
using AiAvatar.Backend.Models;
using AiAvatar.Backend.Options;
using AiAvatar.Backend.Services;
using AiAvatar.Backend.Services.Speech;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5191");

builder.Services.Configure<OllamaOptions>(
    builder.Configuration.GetSection(OllamaOptions.SectionName));

builder.Services.Configure<CharacterOptions>(
    builder.Configuration.GetSection(CharacterOptions.SectionName));

builder.Services.Configure<CosyVoiceOptions>(
    builder.Configuration.GetSection(CosyVoiceOptions.SectionName));

builder.Services.AddHttpClient<OllamaClient>(client =>
{
    // CPU inference can take noticeably longer on the first request while Ollama loads the model.
    client.Timeout = TimeSpan.FromSeconds(120);
});

builder.Services.AddHttpClient<CosyVoiceClient>((serviceProvider, client) =>
{
    var options = serviceProvider
        .GetRequiredService<IOptions<CosyVoiceOptions>>()
        .Value;

    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = Timeout.InfiniteTimeSpan;
});

builder.Services.AddSingleton<CosyVoiceServerHostedService>();
builder.Services.AddSingleton<IHostedService>(serviceProvider =>
    serviceProvider.GetRequiredService<CosyVoiceServerHostedService>());

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
    CosyVoiceClient cosyVoice,
    CancellationToken cancellationToken) =>
{
    var ollamaStatusTask = ollama.GetStatusAsync(cancellationToken);
    var cosyVoiceReadyTask = cosyVoice.IsReadyAsync(cancellationToken);

    await Task.WhenAll(ollamaStatusTask, cosyVoiceReadyTask);

    var ollamaStatus = await ollamaStatusTask;
    var cosyVoiceReady = await cosyVoiceReadyTask;

    return Results.Ok(new
    {
        backend = "ok",
        ollamaReachable = ollamaStatus.Reachable,
        modelInstalled = ollamaStatus.ModelInstalled,
        ollamaMessage = ollamaStatus.Message,
        cosyVoiceReady,
    });
});

app.MapPost("/api/speech", async (
    SpeechSynthesisRequest request,
    CosyVoiceClient cosyVoice,
    IOptions<CosyVoiceOptions> cosyVoiceOptions,
    CancellationToken cancellationToken) =>
{
    var options = cosyVoiceOptions.Value;

    if (!options.Enabled)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Local TTS is disabled",
            detail: "Enable the CosyVoice section in appsettings.json before using avatar speech.");
    }

    if (string.IsNullOrWhiteSpace(request.Text))
    {
        return Results.BadRequest(new { error = "Speech text cannot be empty." });
    }

    if (request.Text.Length > options.MaxTextCharacters)
    {
        return Results.BadRequest(new
        {
            error = $"Speech text exceeds the configured {options.MaxTextCharacters} character limit.",
        });
    }

    if (!TtsSpeechPolicy.IsSupportedEmotion(request.Emotion))
    {
        return Results.BadRequest(new { error = $"Unsupported emotion: {request.Emotion}" });
    }

    try
    {
        var audio = await cosyVoice.SynthesizeAsync(request, cancellationToken);
        return Results.File(audio, "audio/wav");
    }
    catch (HttpRequestException exception)
    {
        return Results.Problem(
            detail: exception.Message,
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Local CosyVoice request failed");
    }
    catch (TaskCanceledException exception)
    {
        return Results.Problem(
            detail: $"Local text-to-speech timed out: {exception.Message}",
            statusCode: StatusCodes.Status504GatewayTimeout,
            title: "CosyVoice synthesis timed out");
    }
    catch (Exception exception)
    {
        return Results.Problem(
            detail: exception.Message,
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Unexpected local TTS error");
    }
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
