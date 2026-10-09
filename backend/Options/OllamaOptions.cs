// This file maps the Ollama section in appsettings.json to strongly typed local-model and inference settings.
namespace AiAvatar.Backend.Options;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string BaseUrl { get; init; } = "http://localhost:11434";
    public string Model { get; init; } = string.Empty;
    public int ContextLength { get; init; } = 4096;
    public double Temperature { get; init; } = 0.55;
    public int MaxOutputTokens { get; init; } = 280;
    public int ContextSafetyReserveTokens { get; init; }
    public string KeepAlive { get; init; } = "10m";
}
