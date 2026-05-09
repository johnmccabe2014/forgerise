using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForgeRise.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SessionSourcePlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceSessionPlanId",
                table: "Sessions",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceSessionPlanId",
                table: "Sessions");
        }
    }
}
