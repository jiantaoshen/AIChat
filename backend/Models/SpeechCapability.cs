// This file exposes backend-owned TTS capability to API consumers without duplicating the supported-language policy in the browser.
namespace AiAvatar.Backend.Models;

public sealed record SpeechCapability(bool Supported);
