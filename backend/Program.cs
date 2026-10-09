// This file is the backend composition root: it configures options, persistence, HTTP clients, centralized ProblemDetails handling, CORS, database startup, and endpoint groups.
using AiAvatar.Backend.Data;
using AiAvatar.Backend.Endpoints;
using AiAvatar.Backend.Infrastructure;
using AiAvatar.Backend.Options;
using AiAvatar.Backend.Services;
using AiAvatar.Backend.Services.Concurrency;
using AiAvatar.Backend.Services.Ollama;
using AiAvatar.Backend.Services.Persistence;
using AiAvatar.Backend.Services.Speech;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5191");

builder.Services
    .AddOptions<OllamaOptions>()
    .Bind(builder.Configuration.GetSection(OllamaOptions.SectionName))
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.Model),
        "Ollama:Model must be configured in backend/appsettings.json or an explicit ASP.NET Core configuration override.")
    .Validate(
        options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _),
        "Ollama:BaseUrl must be an absolute URI.")
    .ValidateOnStart();
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
        Path.Combine(builder.Environment.ContentRootPath, sqliteConnection.DataSource));
}

var databaseDirectory = Path.GetDirectoryName(sqliteConnection.DataSource);
if (!string.IsNullOrWhiteSpace(databaseDirectory))
{
    Directory.CreateDirectory(databaseDirectory);
}

sqliteConnection.ForeignKeys = true;

builder.Services.AddDbContext<AvatarDbContext>(options =>
    options.UseSqlite(sqliteConnection.ConnectionString));
builder.Services.AddScoped<IChatTurnRepository, ChatTurnRepository>();
builder.Services.AddScoped<IConversationHistoryReader, ConversationHistoryReader>();
builder.Services.AddScoped<ISpeechRepository, SpeechRepository>();
builder.Services.AddScoped<ChatTurnService>();
builder.Services.AddSingleton<ConversationTurnGate>();
builder.Services.AddScoped<SpeechSynthesisService>();

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    };
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddSingleton<OllamaRequestFactory>();
builder.Services.AddSingleton<OllamaResponseParser>();
builder.Services.AddHttpClient<OllamaClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(120);
});
builder.Services.AddTransient<IChatDecisionGenerator>(serviceProvider =>
    serviceProvider.GetRequiredService<OllamaClient>());

builder.Services.AddHttpClient<CosyVoiceClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<CosyVoiceOptions>>().Value;
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

app.UseExceptionHandler();
app.UseCors("NextJsDevelopment");
app.MapHealthEndpoints();
app.MapChatEndpoints();
app.MapSpeechEndpoints();

app.Run();
