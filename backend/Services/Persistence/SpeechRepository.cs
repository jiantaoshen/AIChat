// This repository owns speech-specific persistence: authoritative assistant semantics in,
// and TTS telemetry out. It deliberately exposes no chat-turn or model-context operations.
using AiAvatar.Backend.Data;
using AiAvatar.Backend.Data.Entities;
using AiAvatar.Backend.Errors;
using AiAvatar.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace AiAvatar.Backend.Services.Persistence;

public sealed class SpeechRepository(AvatarDbContext db) : ISpeechRepository
{
    public async Task<AssistantSpeechSource?> GetAssistantSpeechSourceAsync(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        return await db.Messages
            .AsNoTracking()
            .Where(item => item.Id == messageId && item.Role == "assistant")
            .Select(item => new AssistantSpeechSource(
                item.Content,
                item.Emotion,
                item.EmotionIntensity))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task SaveTtsTelemetryAsync(
        Guid messageId,
        SpeechSynthesisResult result,
        CancellationToken cancellationToken)
    {
        await EnsureAssistantMessageExistsAsync(messageId, cancellationToken);

        var entity = await db.TtsTelemetry
            .SingleOrDefaultAsync(
                item => item.MessageId == messageId,
                cancellationToken);

        if (entity is null)
        {
            entity = new TtsTelemetryEntity
            {
                MessageId = messageId,
            };
            db.TtsTelemetry.Add(entity);
        }

        entity.Model = result.Model;
        entity.VoiceSource = result.VoiceSource;
        entity.SynthesisDurationMs = result.SynthesisDurationMs;
        entity.AudioDurationMs = null;
        entity.RealTimeFactor = null;
        entity.UsedCuda = result.UsedCuda;
        entity.UsedFp16 = result.UsedFp16;
        entity.CreatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureAssistantMessageExistsAsync(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        var exists = await db.Messages.AnyAsync(
            item => item.Id == messageId && item.Role == "assistant",
            cancellationToken);

        if (!exists)
        {
            throw new ResourceNotFoundException(
                $"Assistant message '{messageId}' was not found in the local database.");
        }
    }
}
