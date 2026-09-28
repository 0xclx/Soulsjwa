using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Soulsjwa.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDisplayNameOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisplayNameOverride",
                table: "Users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TwitchDisplayName",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Every existing name came from Twitch (or is a pending invite's
            // login), so it is the Twitch name; nobody has an override yet.
            migrationBuilder.Sql("UPDATE \"Users\" SET \"TwitchDisplayName\" = \"DisplayName\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayNameOverride",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TwitchDisplayName",
                table: "Users");
        }
    }
}
