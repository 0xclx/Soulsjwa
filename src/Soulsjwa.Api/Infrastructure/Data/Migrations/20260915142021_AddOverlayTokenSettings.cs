using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Soulsjwa.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOverlayTokenSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SettingsJson",
                table: "EventOverlayTokens",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SettingsJson",
                table: "EventOverlayTokens");
        }
    }
}
