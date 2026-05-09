using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForgeRise.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SessionPlanAdoption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AdoptedAt",
                table: "SessionPlans",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AdoptedByUserId",
                table: "SessionPlans",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AdoptedSessionId",
                table: "SessionPlans",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdoptedAt",
                table: "SessionPlans");

            migrationBuilder.DropColumn(
                name: "AdoptedByUserId",
                table: "SessionPlans");

            migrationBuilder.DropColumn(
                name: "AdoptedSessionId",
                table: "SessionPlans");
        }
    }
}
