// This file defines the browser-to-backend speech request. The browser identifies a persisted assistant message; speech text and emotion remain server-authoritative.
namespace AiAvatar.Backend.Models;

public sealed record SpeechSynthesisRequest(Guid MessageId);
