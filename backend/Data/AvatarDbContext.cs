// This DbContext owns the local SQLite persistence model for conversations, messages, and model telemetry.
using AiAvatar.Backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AiAvatar.Backend.Data;

public sealed class AvatarDbContext(DbContextOptions<AvatarDbContext> options)
    : DbContext(options)
{
    public DbSet<ConversationEntity> Conversations => Set<ConversationEntity>();

    public DbSet<MessageEntity> Messages => Set<MessageEntity>();

    public DbSet<LlmTelemetryEntity> LlmTelemetry => Set<LlmTelemetryEntity>();

    public DbSet<TtsTelemetryEntity> TtsTelemetry => Set<TtsTelemetryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConversationEntity>(entity =>
        {
            entity.ToTable("Conversations");
            entity.HasKey(item => item.Id);

            entity.Property(item => item.Title)
                .HasMaxLength(200)
                .IsRequired();

            entity.HasIndex(item => item.UpdatedAtUtc);
        });

        modelBuilder.Entity<MessageEntity>(entity =>
        {
            entity.ToTable("Messages");
            entity.HasKey(item => item.Id);

            entity.Property(item => item.Role)
                .HasMaxLength(16)
                .IsRequired();

            entity.Property(item => item.Content)
                .IsRequired();

            entity.Property(item => item.Language)
                .HasMaxLength(16);

            entity.Property(item => item.Emotion)
                .HasMaxLength(32);

            entity.Property(item => item.Gesture)
                .HasMaxLength(32);

            entity.HasIndex(item => new
            {
                item.ConversationId,
                item.CreatedAtUtc,
            });

            entity.HasIndex(item => new
            {
                item.TurnId,
                item.Role,
            })
                .IsUnique()
                .HasFilter("\"TurnId\" IS NOT NULL");

            entity
                .HasOne(item => item.Conversation)
                .WithMany(conversation => conversation.Messages)
                .HasForeignKey(item => item.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LlmTelemetryEntity>(entity =>
        {
            entity.ToTable("LlmTelemetry");
            entity.HasKey(item => item.Id);

            entity.Property(item => item.Model)
                .HasMaxLength(200)
                .IsRequired();

            entity.HasIndex(item => item.MessageId)
                .IsUnique();

            entity
                .HasOne(item => item.Message)
                .WithOne(message => message.LlmTelemetry)
                .HasForeignKey<LlmTelemetryEntity>(item => item.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TtsTelemetryEntity>(entity =>
        {
            entity.ToTable("TtsTelemetry");
            entity.HasKey(item => item.Id);

            entity.Property(item => item.Model)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(item => item.VoiceSource)
                .HasMaxLength(200);

            entity.HasIndex(item => item.MessageId)
                .IsUnique();

            entity
                .HasOne(item => item.Message)
                .WithOne(message => message.TtsTelemetry)
                .HasForeignKey<TtsTelemetryEntity>(item => item.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
