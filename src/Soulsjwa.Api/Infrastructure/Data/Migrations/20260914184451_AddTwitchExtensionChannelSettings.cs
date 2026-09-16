using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Soulsjwa.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTwitchExtensionChannelSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TwitchExtensionChannelSettings",
                columns: table => new
                {
                    ChannelId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultScope = table.Column<int>(type: "integer", nullable: false),
                    PinnedEventGameId = table.Column<Guid>(type: "uuid", nullable: true),
                    HighlightChannelCompetitor = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ShowTrialProgress = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TwitchExtensionChannelSettings", x => x.ChannelId);
                    table.ForeignKey(
                        name: "FK_TwitchExtensionChannelSettings_EventGames_PinnedEventGameId",
                        column: x => x.PinnedEventGameId,
                        principalTable: "EventGames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TwitchExtensionChannelSettings_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TwitchExtensionChannelSettings_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TwitchExtensionChannelSettings_EventId",
                table: "TwitchExtensionChannelSettings",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_TwitchExtensionChannelSettings_PinnedEventGameId",
                table: "TwitchExtensionChannelSettings",
                column: "PinnedEventGameId");

            migrationBuilder.CreateIndex(
                name: "IX_TwitchExtensionChannelSettings_UpdatedById",
                table: "TwitchExtensionChannelSettings",
                column: "UpdatedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TwitchExtensionChannelSettings");
        }
    }
}
