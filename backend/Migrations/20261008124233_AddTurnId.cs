using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiAvatar.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddTurnId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "Messages",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TurnId",
                table: "Messages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_TurnId_Role",
                table: "Messages",
                columns: new[] { "TurnId", "Role" },
                unique: true,
                filter: "\"TurnId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_TurnId_Role",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "TurnId",
                table: "Messages");
        }
    }
}
