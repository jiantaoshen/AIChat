# Avatar voice reference

Place a clean, licensed reference recording here if you want CosyVoice3 to keep a consistent custom avatar voice.

Required files:

- `reference.wav` — ideally 3–10 seconds, one speaker, little/no music or reverb.
- `reference.txt` — the exact transcript of what is spoken in `reference.wav`.

Example `reference.txt`:

```text
你好，很高兴见到你。今天想聊些什么？
```

If these files are missing and `UseOfficialDemoVoiceWhenReferenceMissing` is enabled in `backend/appsettings.json`, the local service falls back to CosyVoice's bundled demo prompt voice so the TTS pipeline can still be tested.
