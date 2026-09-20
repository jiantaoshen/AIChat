// This file maps the editable character name and personality from appsettings.json into the Qwen system prompt.
namespace AiAvatar.Backend.Options;

public sealed class CharacterOptions
{
    public const string SectionName = "Character";

    public string DisplayName { get; init; } = "Avatar";
    public string Personality { get; init; } = "Calm, observant, concise, and slightly playful.";
}
