using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Soulsjwa.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTwitchExtensionSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TwitchExtensionSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    AllowChannelEventChoice = table.Column<bool>(type: "boolean", nullable: false),
                    AllowViewerScopeSwitch = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultScope = table.Column<int>(type: "integer", nullable: false),
                    DefaultHighlightChannelCompetitor = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultShowTrialProgress = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TwitchExtensionSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TwitchExtensionSettings_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TwitchExtensionSettings_UpdatedById",
                table: "TwitchExtensionSettings",
                column: "UpdatedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TwitchExtensionSettings");
        }
    }
}
