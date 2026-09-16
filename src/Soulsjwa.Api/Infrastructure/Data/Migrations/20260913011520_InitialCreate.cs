using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Soulsjwa.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeatureFlags",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureFlags", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ConnectorSupported = table.Column<bool>(type: "boolean", nullable: false),
                    RequiredConnectorVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LegalDocuments",
                columns: table => new
                {
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalDocuments", x => x.Kind);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TwitchId = table.Column<string>(type: "text", nullable: false),
                    TwitchLogin = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: true),
                    ProfileImageUrl = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    IsAllowlisted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AllowlistedTwitchLogins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TwitchLogin = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AddedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllowlistedTwitchLogins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AllowlistedTwitchLogins_Users_AddedById",
                        column: x => x.AddedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ApiKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    KeyHash = table.Column<string>(type: "text", nullable: false),
                    KeyPrefix = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiKeys_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UrlAlias = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    IsStarted = table.Column<bool>(type: "boolean", nullable: false),
                    IsFeatured = table.Column<bool>(type: "boolean", nullable: false),
                    TieBreakMode = table.Column<int>(type: "integer", nullable: false),
                    AllowTrialRuns = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                    table.CheckConstraint("CK_Events_UrlAlias_Lowercase", "\"UrlAlias\" IS NULL OR \"UrlAlias\" = lower(\"UrlAlias\")");
                    table.ForeignKey(
                        name: "FK_Events_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MediaAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    ByteSize = table.Column<long>(type: "bigint", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaAssets_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    TokenPrefix = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventGameId = table.Column<Guid>(type: "uuid", nullable: true),
                    ObjectiveId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    BeforeJson = table.Column<string>(type: "jsonb", nullable: true),
                    AfterJson = table.Column<string>(type: "jsonb", nullable: true),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_SubjectUserId",
                        column: x => x.SubjectUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "EventCompetitors",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsLive = table.Column<bool>(type: "boolean", nullable: false),
                    IsStreamer = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventCompetitors", x => new { x.EventId, x.UserId });
                    table.ForeignKey(
                        name: "FK_EventCompetitors_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventCompetitors_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EventGames",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    KnownGameId = table.Column<int>(type: "integer", nullable: true),
                    CustomGameName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CustomGameDescription = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventGames", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventGames_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventGames_Games_KnownGameId",
                        column: x => x.KnownGameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EventOverlayTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    TokenPrefix = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventOverlayTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventOverlayTokens_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventOverlayTokens_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EventRules",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventRules", x => x.EventId);
                    table.ForeignKey(
                        name: "FK_EventRules_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CalendarEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DescriptionMarkdown = table.Column<string>(type: "text", nullable: true),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsAllDay = table.Column<bool>(type: "boolean", nullable: false),
                    IsHighlighted = table.Column<bool>(type: "boolean", nullable: false),
                    Color = table.Column<int>(type: "integer", nullable: false),
                    ImageAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarEntries_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CalendarEntries_MediaAssets_ImageAssetId",
                        column: x => x.ImageAssetId,
                        principalTable: "MediaAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalendarEntries_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SiteThemes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BackgroundAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    BackgroundTreatment = table.Column<int>(type: "integer", nullable: false),
                    Font = table.Column<int>(type: "integer", nullable: false),
                    LightDefault = table.Column<string>(type: "text", nullable: false),
                    LightAccent = table.Column<string>(type: "text", nullable: false),
                    LightDanger = table.Column<string>(type: "text", nullable: false),
                    LightInfo = table.Column<string>(type: "text", nullable: false),
                    LightSuccess = table.Column<string>(type: "text", nullable: false),
                    LightHighlight = table.Column<string>(type: "text", nullable: false),
                    DarkDefault = table.Column<string>(type: "text", nullable: false),
                    DarkAccent = table.Column<string>(type: "text", nullable: false),
                    DarkDanger = table.Column<string>(type: "text", nullable: false),
                    DarkInfo = table.Column<string>(type: "text", nullable: false),
                    DarkSuccess = table.Column<string>(type: "text", nullable: false),
                    DarkHighlight = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteThemes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SiteThemes_MediaAssets_BackgroundAssetId",
                        column: x => x.BackgroundAssetId,
                        principalTable: "MediaAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EventCompetitorModerators",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModeratorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventCompetitorModerators", x => new { x.EventId, x.CompetitorUserId, x.ModeratorUserId });
                    table.ForeignKey(
                        name: "FK_EventCompetitorModerators_EventCompetitors_EventId_Competit~",
                        columns: x => new { x.EventId, x.CompetitorUserId },
                        principalTable: "EventCompetitors",
                        principalColumns: new[] { "EventId", "UserId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventCompetitorModerators_Users_ModeratorUserId",
                        column: x => x.ModeratorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EventGameCompetitorInfos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventGameId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventGameCompetitorInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventGameCompetitorInfos_EventGames_EventGameId",
                        column: x => x.EventGameId,
                        principalTable: "EventGames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventGameCompetitorInfos_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventGameCompetitorInfos_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Objectives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventGameId = table.Column<Guid>(type: "uuid", nullable: true),
                    GameId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    Rule = table.Column<string>(type: "jsonb", nullable: true),
                    FailRule = table.Column<string>(type: "jsonb", nullable: true),
                    IsPredefined = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Objectives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Objectives_EventGames_EventGameId",
                        column: x => x.EventGameId,
                        principalTable: "EventGames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Objectives_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlannedRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventGameId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Color = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlannedRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlannedRuns_EventGames_EventGameId",
                        column: x => x.EventGameId,
                        principalTable: "EventGames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlannedRuns_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlannedRuns_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrialRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventGameId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrialRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrialRuns_EventGames_EventGameId",
                        column: x => x.EventGameId,
                        principalTable: "EventGames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrialRuns_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrialRuns_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompletedObjectives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ObjectiveId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TrialRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    InGameTimeMs = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompletedObjectives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompletedObjectives_Objectives_ObjectiveId",
                        column: x => x.ObjectiveId,
                        principalTable: "Objectives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompletedObjectives_TrialRuns_TrialRunId",
                        column: x => x.TrialRunId,
                        principalTable: "TrialRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompletedObjectives_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FailedObjectives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ObjectiveId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FailedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TrialRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    InGameTimeMs = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FailedObjectives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FailedObjectives_Objectives_ObjectiveId",
                        column: x => x.ObjectiveId,
                        principalTable: "Objectives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FailedObjectives_TrialRuns_TrialRunId",
                        column: x => x.TrialRunId,
                        principalTable: "TrialRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FailedObjectives_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "FeatureFlags",
                columns: new[] { "Key", "Enabled" },
                values: new object[] { "myevents.quick_complete.enabled", false });

            migrationBuilder.InsertData(
                table: "Games",
                columns: new[] { "Id", "ConnectorSupported", "Description", "Name", "RequiredConnectorVersion" },
                values: new object[,]
                {
                    { 1, true, "Action RPG known for its challenging gameplay and interconnected world design.", "Dark Souls: Remastered", "3.2.0" },
                    { 2, true, "Sequel featuring a new world with expanded multiplayer and build variety.", "Dark Souls II: Scholar of the First Sin", "3.2.0" },
                    { 3, true, "The final entry in the Dark Souls trilogy with faster combat and refined mechanics.", "Dark Souls III", "3.2.0" },
                    { 6, true, "Shinobi action game focused on sword combat and posture-based mechanics.", "Sekiro: Shadows Die Twice", "3.2.0" },
                    { 9, true, "Open-world action RPG with a vast world co-created with George R.R. Martin. Read live from the running game process via SoulMemory.", "Elden Ring", "3.3.0" }
                });

            migrationBuilder.InsertData(
                table: "SiteThemes",
                columns: new[] { "Id", "BackgroundAssetId", "BackgroundTreatment", "DarkAccent", "DarkDanger", "DarkDefault", "DarkHighlight", "DarkInfo", "DarkSuccess", "Font", "LightAccent", "LightDanger", "LightDefault", "LightHighlight", "LightInfo", "LightSuccess", "UpdatedAt" },
                values: new object[] { 1, null, 3, "#c4b5fd", "#f87171", "#1e1e1e", "#fbbf24", "#38bdf8", "#4ade80", 0, "#6d28d9", "#b91c1c", "#f5f5f5", "#b45309", "#0369a1", "#15803d", new DateTime(2026, 9, 11, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "IX_AllowlistedTwitchLogins_AddedById",
                table: "AllowlistedTwitchLogins",
                column: "AddedById");

            migrationBuilder.CreateIndex(
                name: "IX_AllowlistedTwitchLogins_TwitchLogin",
                table: "AllowlistedTwitchLogins",
                column: "TwitchLogin",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_KeyPrefix",
                table: "ApiKeys",
                column: "KeyPrefix");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_UserId",
                table: "ApiKeys",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorUserId",
                table: "AuditLogs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAt",
                table: "AuditLogs",
                columns: new[] { "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EventGameId",
                table: "AuditLogs",
                column: "EventGameId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EventId_CreatedAt",
                table: "AuditLogs",
                columns: new[] { "EventId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ObjectiveId",
                table: "AuditLogs",
                column: "ObjectiveId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_SubjectUserId",
                table: "AuditLogs",
                column: "SubjectUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Type",
                table: "AuditLogs",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEntries_CreatedById",
                table: "CalendarEntries",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEntries_EventId_StartsAt",
                table: "CalendarEntries",
                columns: new[] { "EventId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEntries_ImageAssetId",
                table: "CalendarEntries",
                column: "ImageAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEntries_StartsAt",
                table: "CalendarEntries",
                column: "StartsAt");

            migrationBuilder.CreateIndex(
                name: "IX_CompletedObjectives_ObjectiveId_UserId_Official",
                table: "CompletedObjectives",
                columns: new[] { "ObjectiveId", "UserId" },
                unique: true,
                filter: "\"TrialRunId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CompletedObjectives_ObjectiveId_UserId_TrialRunId",
                table: "CompletedObjectives",
                columns: new[] { "ObjectiveId", "UserId", "TrialRunId" },
                unique: true,
                filter: "\"TrialRunId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CompletedObjectives_TrialRunId",
                table: "CompletedObjectives",
                column: "TrialRunId");

            migrationBuilder.CreateIndex(
                name: "IX_CompletedObjectives_UserId",
                table: "CompletedObjectives",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_EventCompetitorModerators_EventId_ModeratorUserId",
                table: "EventCompetitorModerators",
                columns: new[] { "EventId", "ModeratorUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_EventCompetitorModerators_ModeratorUserId",
                table: "EventCompetitorModerators",
                column: "ModeratorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EventCompetitors_EventId_IsStreamer",
                table: "EventCompetitors",
                columns: new[] { "EventId", "IsStreamer" });

            migrationBuilder.CreateIndex(
                name: "IX_EventCompetitors_UserId",
                table: "EventCompetitors",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_EventGameCompetitorInfos_CreatedById",
                table: "EventGameCompetitorInfos",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_EventGameCompetitorInfos_EventGameId_UserId",
                table: "EventGameCompetitorInfos",
                columns: new[] { "EventGameId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_EventGameCompetitorInfos_UserId",
                table: "EventGameCompetitorInfos",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_EventGames_EventId_ActiveGame",
                table: "EventGames",
                column: "EventId",
                unique: true,
                filter: "\"IsEnabled\"");

            migrationBuilder.CreateIndex(
                name: "IX_EventGames_EventId_KnownGameId",
                table: "EventGames",
                columns: new[] { "EventId", "KnownGameId" },
                filter: "\"KnownGameId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EventGames_KnownGameId",
                table: "EventGames",
                column: "KnownGameId");

            migrationBuilder.CreateIndex(
                name: "IX_EventOverlayTokens_CreatedById",
                table: "EventOverlayTokens",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_EventOverlayTokens_EventId_TokenPrefix",
                table: "EventOverlayTokens",
                columns: new[] { "EventId", "TokenPrefix" });

            migrationBuilder.CreateIndex(
                name: "IX_Events_CreatedAt",
                table: "Events",
                column: "CreatedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Events_CreatedById",
                table: "Events",
                column: "CreatedById");

            // Required by the gin_trgm_ops operator class the two GIN indexes below
            // use. Needs CREATE privilege on the database — not superuser on most
            // managed Postgres providers, which allowlist pg_trgm.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            migrationBuilder.CreateIndex(
                name: "IX_Events_Description_Trgm",
                table: "Events",
                column: "Description")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Events_FeaturedEvent",
                table: "Events",
                column: "IsFeatured",
                unique: true,
                filter: "\"IsFeatured\"");

            migrationBuilder.CreateIndex(
                name: "IX_Events_Name_Trgm",
                table: "Events",
                column: "Name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Events_UrlAlias",
                table: "Events",
                column: "UrlAlias",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FailedObjectives_ObjectiveId_UserId_Official",
                table: "FailedObjectives",
                columns: new[] { "ObjectiveId", "UserId" },
                unique: true,
                filter: "\"TrialRunId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FailedObjectives_ObjectiveId_UserId_TrialRunId",
                table: "FailedObjectives",
                columns: new[] { "ObjectiveId", "UserId", "TrialRunId" },
                unique: true,
                filter: "\"TrialRunId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FailedObjectives_TrialRunId",
                table: "FailedObjectives",
                column: "TrialRunId");

            migrationBuilder.CreateIndex(
                name: "IX_FailedObjectives_UserId",
                table: "FailedObjectives",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_CreatedById",
                table: "MediaAssets",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_Sha256",
                table: "MediaAssets",
                column: "Sha256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Objectives_EventGameId",
                table: "Objectives",
                column: "EventGameId");

            migrationBuilder.CreateIndex(
                name: "IX_Objectives_GameId",
                table: "Objectives",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_PlannedRuns_EventGameId",
                table: "PlannedRuns",
                column: "EventGameId");

            migrationBuilder.CreateIndex(
                name: "IX_PlannedRuns_EventId_StartsAt",
                table: "PlannedRuns",
                columns: new[] { "EventId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PlannedRuns_StartsAt",
                table: "PlannedRuns",
                column: "StartsAt");

            migrationBuilder.CreateIndex(
                name: "IX_PlannedRuns_UserId_StartsAt",
                table: "PlannedRuns",
                columns: new[] { "UserId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_ExpiresAt",
                table: "RefreshTokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenPrefix",
                table: "RefreshTokens",
                column: "TokenPrefix");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteThemes_BackgroundAssetId",
                table: "SiteThemes",
                column: "BackgroundAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_TrialRuns_EventGameId_UserId",
                table: "TrialRuns",
                columns: new[] { "EventGameId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrialRuns_EventId",
                table: "TrialRuns",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_TrialRuns_UserId",
                table: "TrialRuns",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TwitchId",
                table: "Users",
                column: "TwitchId",
                unique: true);

            // Functional (expression) index: EF's fluent API can't declare an index
            // over LOWER("TwitchLogin"), so this is hand-written SQL with no model
            // change. Non-unique because PendingUserMarker placeholder rows
            // legitimately reuse a login, and that must not fail a migration.
            migrationBuilder.Sql(
                "CREATE INDEX \"IX_Users_TwitchLogin_Lower\" ON \"Users\" (LOWER(\"TwitchLogin\"));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AllowlistedTwitchLogins");

            migrationBuilder.DropTable(
                name: "ApiKeys");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "CalendarEntries");

            migrationBuilder.DropTable(
                name: "CompletedObjectives");

            migrationBuilder.DropTable(
                name: "EventCompetitorModerators");

            migrationBuilder.DropTable(
                name: "EventGameCompetitorInfos");

            migrationBuilder.DropTable(
                name: "EventOverlayTokens");

            migrationBuilder.DropTable(
                name: "EventRules");

            migrationBuilder.DropTable(
                name: "FailedObjectives");

            migrationBuilder.DropTable(
                name: "FeatureFlags");

            migrationBuilder.DropTable(
                name: "LegalDocuments");

            migrationBuilder.DropTable(
                name: "PlannedRuns");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "SiteThemes");

            migrationBuilder.DropTable(
                name: "EventCompetitors");

            migrationBuilder.DropTable(
                name: "Objectives");

            migrationBuilder.DropTable(
                name: "TrialRuns");

            migrationBuilder.DropTable(
                name: "MediaAssets");

            migrationBuilder.DropTable(
                name: "EventGames");

            migrationBuilder.DropTable(
                name: "Events");

            migrationBuilder.DropTable(
                name: "Games");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
