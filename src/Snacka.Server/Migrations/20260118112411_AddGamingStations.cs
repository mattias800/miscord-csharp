using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snacka.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddGamingStations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GamingStations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    MachineId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ConnectionId = table.Column<string>(type: "text", nullable: true),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GamingStations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GamingStations_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StationAccessGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Permission = table.Column<int>(type: "integer", nullable: false),
                    GrantedById = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StationAccessGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StationAccessGrants_GamingStations_StationId",
                        column: x => x.StationId,
                        principalTable: "GamingStations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StationAccessGrants_Users_GrantedById",
                        column: x => x.GrantedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StationAccessGrants_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StationSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StationSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StationSessions_GamingStations_StationId",
                        column: x => x.StationId,
                        principalTable: "GamingStations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StationSessionUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<string>(type: "text", nullable: true),
                    PlayerSlot = table.Column<int>(type: "integer", nullable: true),
                    InputMode = table.Column<int>(type: "integer", nullable: false),
                    ConnectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DisconnectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastInputAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StationSessionUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StationSessionUsers_StationSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "StationSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StationSessionUsers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GamingStations_ConnectionId",
                table: "GamingStations",
                column: "ConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_GamingStations_OwnerId",
                table: "GamingStations",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_GamingStations_OwnerId_MachineId",
                table: "GamingStations",
                columns: new[] { "OwnerId", "MachineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StationAccessGrants_GrantedById",
                table: "StationAccessGrants",
                column: "GrantedById");

            migrationBuilder.CreateIndex(
                name: "IX_StationAccessGrants_StationId_UserId",
                table: "StationAccessGrants",
                columns: new[] { "StationId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StationAccessGrants_UserId",
                table: "StationAccessGrants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_StationSessions_StationId_EndedAt",
                table: "StationSessions",
                columns: new[] { "StationId", "EndedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StationSessionUsers_ConnectionId",
                table: "StationSessionUsers",
                column: "ConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_StationSessionUsers_SessionId_UserId",
                table: "StationSessionUsers",
                columns: new[] { "SessionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StationSessionUsers_UserId",
                table: "StationSessionUsers",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StationAccessGrants");

            migrationBuilder.DropTable(
                name: "StationSessionUsers");

            migrationBuilder.DropTable(
                name: "StationSessions");

            migrationBuilder.DropTable(
                name: "GamingStations");
        }
    }
}
