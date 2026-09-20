// This file maps the Ollama section in appsettings.json to strongly typed local-model and inference settings.
namespace AiAvatar.Backend.Options;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string BaseUrl { get; init; } = "http://localhost:11434";
    public string Model { get; init; } = "qwen3:4b-instruct-2507-q4_K_M";
    public int ContextLength { get; init; } = 4096;
    public double Temperature { get; init; } = 0.55;
    public int MaxOutputTokens { get; init; } = 280;
    public string KeepAlive { get; init; } = "10m";
}
