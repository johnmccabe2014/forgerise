using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForgeRise.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DrillPrefLastChangedBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LastChangedByUserId",
                table: "TeamDrillPreferences",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastChangedByUserId",
                table: "TeamDrillPreferences");
        }
    }
}
