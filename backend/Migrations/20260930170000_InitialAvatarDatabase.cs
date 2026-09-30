// This migration creates the initial SQLite schema for conversations, messages, LLM telemetry, and TTS telemetry.
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiAvatar.Backend.Migrations;

public partial class InitialAvatarDatabase : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Conversations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Conversations", item => item.Id);
            });

        migrationBuilder.CreateTable(
            name: "Messages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ConversationId = table.Column<Guid>(type: "TEXT", nullable: false),
                Role = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                Content = table.Column<string>(type: "TEXT", nullable: false),
                Emotion = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                EmotionIntensity = table.Column<double>(type: "REAL", nullable: true),
                Gesture = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                GestureIntensity = table.Column<double>(type: "REAL", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Messages", item => item.Id);
                table.ForeignKey(
                    name: "FK_Messages_Conversations_ConversationId",
                    column: item => item.ConversationId,
                    principalTable: "Conversations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "LlmTelemetry",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                MessageId = table.Column<Guid>(type: "TEXT", nullable: false),
                Model = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                TotalDurationMs = table.Column<double>(type: "REAL", nullable: true),
                LoadDurationMs = table.Column<double>(type: "REAL", nullable: true),
                PromptTokens = table.Column<int>(type: "INTEGER", nullable: true),
                OutputTokens = table.Column<int>(type: "INTEGER", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LlmTelemetry", item => item.Id);
                table.ForeignKey(
                    name: "FK_LlmTelemetry_Messages_MessageId",
                    column: item => item.MessageId,
                    principalTable: "Messages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "TtsTelemetry",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                MessageId = table.Column<Guid>(type: "TEXT", nullable: false),
                Model = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                VoiceSource = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                SynthesisDurationMs = table.Column<long>(type: "INTEGER", nullable: false),
                AudioDurationMs = table.Column<long>(type: "INTEGER", nullable: true),
                RealTimeFactor = table.Column<double>(type: "REAL", nullable: true),
                UsedCuda = table.Column<bool>(type: "INTEGER", nullable: true),
                UsedFp16 = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TtsTelemetry", item => item.Id);
                table.ForeignKey(
                    name: "FK_TtsTelemetry_Messages_MessageId",
                    column: item => item.MessageId,
                    principalTable: "Messages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Conversations_UpdatedAtUtc",
            table: "Conversations",
            column: "UpdatedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_Messages_ConversationId_CreatedAtUtc",
            table: "Messages",
            columns: new[] { "ConversationId", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_LlmTelemetry_MessageId",
            table: "LlmTelemetry",
            column: "MessageId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TtsTelemetry_MessageId",
            table: "TtsTelemetry",
            column: "MessageId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "LlmTelemetry");
        migrationBuilder.DropTable(name: "TtsTelemetry");
        migrationBuilder.DropTable(name: "Messages");
        migrationBuilder.DropTable(name: "Conversations");
    }
}
