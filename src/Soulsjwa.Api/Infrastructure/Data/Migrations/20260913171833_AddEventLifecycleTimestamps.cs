using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Soulsjwa.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventLifecycleTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "Events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StoppedAt",
                table: "Events",
                type: "timestamp with time zone",
                nullable: true);

            // Backfill from the audit trail while it is still there: My Events
            // used to reconstruct "stopped" from these rows, and an install
            // whose retention has not yet purged them keeps the same answer.
            migrationBuilder.Sql("""
                UPDATE "Events" e
                SET "StartedAt" = (
                    SELECT max(a."CreatedAt") FROM "AuditLogs" a
                    WHERE a."EventId" = e."Id" AND a."Type" = 'event.started')
                WHERE EXISTS (
                    SELECT 1 FROM "AuditLogs" a
                    WHERE a."EventId" = e."Id" AND a."Type" = 'event.started');

                UPDATE "Events" e
                SET "StoppedAt" = (
                    SELECT max(a."CreatedAt") FROM "AuditLogs" a
                    WHERE a."EventId" = e."Id" AND a."Type" = 'event.stopped')
                WHERE e."IsStarted" = FALSE AND EXISTS (
                    SELECT 1 FROM "AuditLogs" a
                    WHERE a."EventId" = e."Id" AND a."Type" = 'event.stopped');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "StoppedAt",
                table: "Events");
        }
    }
}
