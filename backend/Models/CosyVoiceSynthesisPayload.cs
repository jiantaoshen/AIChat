// This file defines the deterministic backend-to-CosyVoice payload after emotion has been converted into safe TTS controls.
namespace AiAvatar.Backend.Models;

public sealed record CosyVoiceSynthesisPayload(
    string Text,
    string Instruction,
    double Speed);
