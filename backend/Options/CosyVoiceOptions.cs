// This file defines configuration for the local CosyVoice3 text-to-speech service, including the project-local Python venv used to launch it.
namespace AiAvatar.Backend.Options;

public sealed class CosyVoiceOptions
{
    public const string SectionName = "CosyVoice";

    public bool Enabled { get; set; } = true;
    public bool AutoStart { get; set; } = true;
    public string PythonExecutable { get; set; } = "../tools/cosyvoice/.venv/Scripts/python.exe";
    public string ServiceScript { get; set; } = "../tools/cosyvoice-service/server.py";
    public string CosyVoiceRepo { get; set; } = "../tools/cosyvoice/CosyVoice";
    public string ModelPath { get; set; } = "../tools/cosyvoice/models/Fun-CosyVoice3-0.5B-2512";
    public string ReferenceAudioPath { get; set; } = "../tools/cosyvoice/voice/reference.wav";
    public string ReferenceTextPath { get; set; } = "../tools/cosyvoice/voice/reference.txt";
    public bool UseOfficialDemoVoiceWhenReferenceMissing { get; set; } = true;
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 8188;
    public int StartupTimeoutSeconds { get; set; } = 240;
    public int SynthesisTimeoutSeconds { get; set; } = 180;
    public int MaxTextCharacters { get; set; } = 800;

    public string BaseUrl => $"http://{Host}:{Port}";
}
