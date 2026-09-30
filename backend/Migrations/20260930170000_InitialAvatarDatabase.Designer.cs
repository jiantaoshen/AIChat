// This generated migration metadata describes the initial SQLite persistence model for EF Core.
using System;
using AiAvatar.Backend.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiAvatar.Backend.Migrations;

[DbContext(typeof(AvatarDbContext))]
[Migration("20260930170000_InitialAvatarDatabase")]
partial class InitialAvatarDatabase
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder.HasAnnotation("ProductVersion", "10.0.12");

        modelBuilder.Entity("AiAvatar.Backend.Data.Entities.ConversationEntity", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("TEXT");

            entity.Property<DateTime>("CreatedAtUtc")
                .HasColumnType("TEXT");

            entity.Property<string>("Title")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("TEXT");

            entity.Property<DateTime>("UpdatedAtUtc")
                .HasColumnType("TEXT");

            entity.HasKey("Id");
            entity.HasIndex("UpdatedAtUtc");
            entity.ToTable("Conversations");
        });

        modelBuilder.Entity("AiAvatar.Backend.Data.Entities.MessageEntity", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("TEXT");

            entity.Property<string>("Content")
                .IsRequired()
                .HasColumnType("TEXT");

            entity.Property<Guid>("ConversationId")
                .HasColumnType("TEXT");

            entity.Property<DateTime>("CreatedAtUtc")
                .HasColumnType("TEXT");

            entity.Property<string>("Emotion")
                .HasMaxLength(32)
                .HasColumnType("TEXT");

            entity.Property<double?>("EmotionIntensity")
                .HasColumnType("REAL");

            entity.Property<string>("Gesture")
                .HasMaxLength(32)
                .HasColumnType("TEXT");

            entity.Property<double?>("GestureIntensity")
                .HasColumnType("REAL");

            entity.Property<string>("Role")
                .IsRequired()
                .HasMaxLength(16)
                .HasColumnType("TEXT");

            entity.HasKey("Id");
            entity.HasIndex("ConversationId", "CreatedAtUtc");
            entity.ToTable("Messages");
        });

        modelBuilder.Entity("AiAvatar.Backend.Data.Entities.LlmTelemetryEntity", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("TEXT");

            entity.Property<DateTime>("CreatedAtUtc")
                .HasColumnType("TEXT");

            entity.Property<double?>("LoadDurationMs")
                .HasColumnType("REAL");

            entity.Property<Guid>("MessageId")
                .HasColumnType("TEXT");

            entity.Property<string>("Model")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("TEXT");

            entity.Property<int?>("OutputTokens")
                .HasColumnType("INTEGER");

            entity.Property<int?>("PromptTokens")
                .HasColumnType("INTEGER");

            entity.Property<double?>("TotalDurationMs")
                .HasColumnType("REAL");

            entity.HasKey("Id");
            entity.HasIndex("MessageId").IsUnique();
            entity.ToTable("LlmTelemetry");
        });

        modelBuilder.Entity("AiAvatar.Backend.Data.Entities.TtsTelemetryEntity", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("TEXT");

            entity.Property<long?>("AudioDurationMs")
                .HasColumnType("INTEGER");

            entity.Property<DateTime>("CreatedAtUtc")
                .HasColumnType("TEXT");

            entity.Property<Guid>("MessageId")
                .HasColumnType("TEXT");

            entity.Property<string>("Model")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("TEXT");

            entity.Property<double?>("RealTimeFactor")
                .HasColumnType("REAL");

            entity.Property<long>("SynthesisDurationMs")
                .HasColumnType("INTEGER");

            entity.Property<bool?>("UsedCuda")
                .HasColumnType("INTEGER");

            entity.Property<bool>("UsedFp16")
                .HasColumnType("INTEGER");

            entity.Property<string>("VoiceSource")
                .HasMaxLength(200)
                .HasColumnType("TEXT");

            entity.HasKey("Id");
            entity.HasIndex("MessageId").IsUnique();
            entity.ToTable("TtsTelemetry");
        });

        modelBuilder.Entity("AiAvatar.Backend.Data.Entities.MessageEntity", entity =>
        {
            entity.HasOne("AiAvatar.Backend.Data.Entities.ConversationEntity", "Conversation")
                .WithMany("Messages")
                .HasForeignKey("ConversationId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            entity.Navigation("Conversation");
        });

        modelBuilder.Entity("AiAvatar.Backend.Data.Entities.LlmTelemetryEntity", entity =>
        {
            entity.HasOne("AiAvatar.Backend.Data.Entities.MessageEntity", "Message")
                .WithOne("LlmTelemetry")
                .HasForeignKey("AiAvatar.Backend.Data.Entities.LlmTelemetryEntity", "MessageId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            entity.Navigation("Message");
        });

        modelBuilder.Entity("AiAvatar.Backend.Data.Entities.TtsTelemetryEntity", entity =>
        {
            entity.HasOne("AiAvatar.Backend.Data.Entities.MessageEntity", "Message")
                .WithOne("TtsTelemetry")
                .HasForeignKey("AiAvatar.Backend.Data.Entities.TtsTelemetryEntity", "MessageId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            entity.Navigation("Message");
        });

        modelBuilder.Entity("AiAvatar.Backend.Data.Entities.ConversationEntity", entity =>
        {
            entity.Navigation("Messages");
        });

        modelBuilder.Entity("AiAvatar.Backend.Data.Entities.MessageEntity", entity =>
        {
            entity.Navigation("LlmTelemetry");
            entity.Navigation("TtsTelemetry");
        });
#pragma warning restore 612, 618
    }
}
