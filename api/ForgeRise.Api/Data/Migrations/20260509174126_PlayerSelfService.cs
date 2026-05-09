using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForgeRise.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PlayerSelfService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SubmittedBySelf",
                table: "WellnessCheckIns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PlayerInvites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConsumedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerInvites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerInvites_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerLinks_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerLinks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerInvites_Code",
                table: "PlayerInvites",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerInvites_PlayerId",
                table: "PlayerInvites",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerLinks_PlayerId_UserId",
                table: "PlayerLinks",
                columns: new[] { "PlayerId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerLinks_UserId",
                table: "PlayerLinks",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerInvites");

            migrationBuilder.DropTable(
                name: "PlayerLinks");

            migrationBuilder.DropColumn(
                name: "SubmittedBySelf",
                table: "WellnessCheckIns");
        }
    }
}
