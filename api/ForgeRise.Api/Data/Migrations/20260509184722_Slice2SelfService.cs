using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForgeRise.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Slice2SelfService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GuardianAcknowledgedAt",
                table: "PlayerInvites",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GuardianAcknowledgedByUserId",
                table: "PlayerInvites",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SubmittedBySelf",
                table: "IncidentReports",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GuardianAcknowledgedAt",
                table: "PlayerInvites");

            migrationBuilder.DropColumn(
                name: "GuardianAcknowledgedByUserId",
                table: "PlayerInvites");

            migrationBuilder.DropColumn(
                name: "SubmittedBySelf",
                table: "IncidentReports");
        }
    }
}
