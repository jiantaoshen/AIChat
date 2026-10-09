using AiAvatar.Backend.Data.Entities;
using AiAvatar.Backend.Models;
using AiAvatar.Backend.Services.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiAvatar.Backend.Tests;

public sealed class SpeechRepositoryTests
{
    [Fact]
    public async Task GetAssistantSpeechSourceAsync_ReturnsPersistedContentAndEmotion()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await PersistenceTestFixture.CreateAsync(cancellationToken);
        var conversation = new ConversationEntity { Title = "speech authority" };
        fixture.Db.Conversations.Add(conversation);

        var assistant = new MessageEntity
        {
            ConversationId = conversation.Id,
            TurnId = Guid.NewGuid(),
            Role = "assistant",
            Content = "persisted authoritative speech",
            Language = "en",
            Emotion = "sad",
            EmotionIntensity = 0.75,
        };
        fixture.Db.Messages.Add(assistant);
        await fixture.Db.SaveChangesAsync(cancellationToken);

        var repository = new SpeechRepository(fixture.Db);
        var source = await repository.GetAssistantSpeechSourceAsync(
            assistant.Id,
            cancellationToken);

        Assert.NotNull(source);
        Assert.Equal("persisted authoritative speech", source.Text);
        Assert.Equal("sad", source.Emotion);
        Assert.Equal(0.75, source.EmotionIntensity);
    }

    [Fact]
    public async Task SaveTtsTelemetryAsync_PersistsTelemetryForAssistantMessage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await PersistenceTestFixture.CreateAsync(cancellationToken);
        var conversation = new ConversationEntity { Title = "tts telemetry" };
        fixture.Db.Conversations.Add(conversation);

        var assistant = new MessageEntity
        {
            ConversationId = conversation.Id,
            TurnId = Guid.NewGuid(),
            Role = "assistant",
            Content = "hello",
            Emotion = "neutral",
            EmotionIntensity = 0.2,
        };
        fixture.Db.Messages.Add(assistant);
        await fixture.Db.SaveChangesAsync(cancellationToken);

        var repository = new SpeechRepository(fixture.Db);
        await repository.SaveTtsTelemetryAsync(
            assistant.Id,
            new SpeechSynthesisResult(
                [1, 2, 3],
                SynthesisDurationMs: 25,
                Model: "test-tts",
                VoiceSource: "reference.wav",
                UsedCuda: false,
                UsedFp16: false),
            cancellationToken);

        var telemetry = await fixture.Db.TtsTelemetry
            .SingleAsync(item => item.MessageId == assistant.Id, cancellationToken);
        Assert.Equal("test-tts", telemetry.Model);
        Assert.Equal("reference.wav", telemetry.VoiceSource);
        Assert.Equal(25, telemetry.SynthesisDurationMs);
    }
}
