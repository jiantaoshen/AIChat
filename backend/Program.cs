// This file wires the ASP.NET Core 10 API, SQLite persistence, CORS, local Ollama/Qwen chat, CosyVoice3 avatar TTS, health checks, and request validation.
using AiAvatar.Backend.Data;
using AiAvatar.Backend.Models;
using AiAvatar.Backend.Options;
using AiAvatar.Backend.Services;
using AiAvatar.Backend.Services.Persistence;
using AiAvatar.Backend.Services.Speech;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5191");

builder.Services.Configure<OllamaOptions>(
    builder.Configuration.GetSection(OllamaOptions.SectionName));

builder.Services.Configure<CharacterOptions>(
    builder.Configuration.GetSection(CharacterOptions.SectionName));

builder.Services.Configure<CosyVoiceOptions>(
    builder.Configuration.GetSection(CosyVoiceOptions.SectionName));

var configuredDatabase = builder.Configuration.GetConnectionString("AvatarDatabase")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:AvatarDatabase is missing from appsettings.json.");

var sqliteConnection = new SqliteConnectionStringBuilder(configuredDatabase);
if (!Path.IsPathRooted(sqliteConnection.DataSource))
{
    sqliteConnection.DataSource = Path.GetFullPath(
        Path.Combine(
            builder.Environment.ContentRootPath,
            sqliteConnection.DataSource));
}

var databaseDirectory = Path.GetDirectoryName(sqliteConnection.DataSource);
if (!string.IsNullOrWhiteSpace(databaseDirectory))
{
    Directory.CreateDirectory(databaseDirectory);
}

sqliteConnection.ForeignKeys = true;

builder.Services.AddDbContext<AvatarDbContext>(options =>
    options.UseSqlite(sqliteConnection.ConnectionString));

builder.Services.AddScoped<IConversationStore, ConversationStore>();

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

await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<AvatarDbContext>();
    await database.Database.MigrateAsync();
    await database.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
}

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
        database = "sqlite",
        ollamaReachable = ollamaStatus.Reachable,
        modelInstalled = ollamaStatus.ModelInstalled,
        ollamaMessage = ollamaStatus.Message,
        cosyVoiceReady,
    });
});

app.MapPost("/api/speech", async (
    SpeechSynthesisRequest request,
    CosyVoiceClient cosyVoice,
    IConversationStore conversationStore,
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

    if (request.MessageId == Guid.Empty)
    {
        return Results.BadRequest(new { error = "A persisted assistant messageId is required for speech." });
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

    if (!await conversationStore.AssistantMessageExistsAsync(
            request.MessageId,
            cancellationToken))
    {
        return Results.NotFound(new
        {
            error = $"Assistant message '{request.MessageId}' was not found in the local database.",
        });
    }

    try
    {
        var result = await cosyVoice.SynthesizeAsync(request, cancellationToken);

        await conversationStore.SaveTtsTelemetryAsync(
            request.MessageId,
            result,
            cancellationToken);

        return Results.File(result.Audio, "audio/wav");
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new { error = exception.Message });
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
    IConversationStore conversationStore,
    CancellationToken cancellationToken) =>
{
    var validationError = ChatRequestValidator.Validate(request);
    if (validationError is not null)
    {
        return Results.BadRequest(new { error = validationError });
    }

    var latestUserMessage = request.Messages[^1];

    try
    {
        var conversation = await conversationStore.GetOrCreateConversationAsync(
            request.ConversationId,
            latestUserMessage.Content,
            cancellationToken);

        await conversationStore.AddUserMessageAsync(
            conversation.Id,
            latestUserMessage.Content,
            cancellationToken);

        var ollamaResult = await ollama.CreateDecisionAsync(
            request.Messages,
            cancellationToken);

        var assistantMessage = await conversationStore.AddAssistantMessageAsync(
            conversation.Id,
            ollamaResult.Decision,
            cancellationToken);

        await conversationStore.SaveLlmTelemetryAsync(
            assistantMessage.Id,
            ollamaResult.Telemetry,
            cancellationToken);

        return Results.Ok(new AvatarChatResponse(
            conversation.Id,
            assistantMessage.Id,
            ollamaResult.Decision,
            ollamaResult.Telemetry));
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new { error = exception.Message });
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
