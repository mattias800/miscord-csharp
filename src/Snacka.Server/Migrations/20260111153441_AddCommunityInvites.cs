using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snacka.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityInvites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ScreenShareHasAudio",
                table: "VoiceParticipants",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CommunityInvites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CommunityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    InvitedUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    InvitedById = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityInvites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityInvites_Communities_CommunityId",
                        column: x => x.CommunityId,
                        principalTable: "Communities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CommunityInvites_Users_InvitedById",
                        column: x => x.InvitedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CommunityInvites_Users_InvitedUserId",
                        column: x => x.InvitedUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityInvites_CommunityId_InvitedUserId_Status",
                table: "CommunityInvites",
                columns: new[] { "CommunityId", "InvitedUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityInvites_InvitedById",
                table: "CommunityInvites",
                column: "InvitedById");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityInvites_InvitedUserId",
                table: "CommunityInvites",
                column: "InvitedUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunityInvites");

            migrationBuilder.DropColumn(
                name: "ScreenShareHasAudio",
                table: "VoiceParticipants");
        }
    }
}
