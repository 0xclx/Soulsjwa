using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Features.Admin;
using Soulsjwa.Api.Features.Admin.Entities;
using Soulsjwa.Api.Features.Audits.Entities;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Calendar.Entities;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Features.Games.Entities;
using Soulsjwa.Api.Features.Legal.Entities;
using Soulsjwa.Api.Features.Media.Entities;
using Soulsjwa.Api.Features.Theme.Entities;
using Soulsjwa.Api.Features.TwitchExtension.Entities;

namespace Soulsjwa.Api.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<AllowlistedTwitchLogin> AllowlistedTwitchLogins => Set<AllowlistedTwitchLogin>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventCompetitor> EventCompetitors => Set<EventCompetitor>();
    public DbSet<EventGame> EventGames => Set<EventGame>();
    public DbSet<EventCompetitorModerator> EventCompetitorModerators => Set<EventCompetitorModerator>();
    public DbSet<Objective> Objectives => Set<Objective>();
    public DbSet<CompletedObjective> CompletedObjectives => Set<CompletedObjective>();
    public DbSet<FailedObjective> FailedObjectives => Set<FailedObjective>();
    public DbSet<EventGameCompetitorInfo> EventGameCompetitorInfos => Set<EventGameCompetitorInfo>();
    public DbSet<TrialRun> TrialRuns => Set<TrialRun>();
    public DbSet<CalendarEntry> CalendarEntries => Set<CalendarEntry>();
    public DbSet<PlannedRun> PlannedRuns => Set<PlannedRun>();
    public DbSet<EventOverlayToken> EventOverlayTokens => Set<EventOverlayToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<EventRules> EventRules => Set<EventRules>();
    public DbSet<LegalDocument> LegalDocuments => Set<LegalDocument>();
    public DbSet<SiteTheme> SiteThemes => Set<SiteTheme>();
    public DbSet<TwitchExtensionChannelSettings> TwitchExtensionChannelSettings => Set<TwitchExtensionChannelSettings>();
    public DbSet<TwitchExtensionSettings> TwitchExtensionSettings => Set<TwitchExtensionSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TwitchId).IsUnique();
            e.Property(x => x.TwitchId).IsRequired();
            e.Property(x => x.TwitchLogin).IsRequired();
            e.Property(x => x.DisplayName).IsRequired();
            e.Property(x => x.Role).HasConversion<int>();
        });

        modelBuilder.Entity<AllowlistedTwitchLogin>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.TwitchLogin).IsRequired().HasMaxLength(64);
            e.HasIndex(x => x.TwitchLogin).IsUnique();
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasOne(x => x.AddedBy)
                .WithMany()
                .HasForeignKey(x => x.AddedById)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TokenPrefix);
            // Drives RetentionService's expired-token sweep; without it the
            // delete is a sequential scan of a table that only grows.
            e.HasIndex(x => x.ExpiresAt).HasDatabaseName("IX_RefreshTokens_ExpiresAt");
            e.Property(x => x.TokenHash).IsRequired();
            e.Property(x => x.TokenPrefix).IsRequired();
            e.HasOne(x => x.User)
                .WithMany(x => x.RefreshTokens)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApiKey>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.KeyPrefix);
            e.Property(x => x.KeyHash).IsRequired();
            e.Property(x => x.Name).IsRequired();
            e.HasOne(x => x.User)
                .WithMany(x => x.ApiKeys)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Game>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.RequiredConnectorVersion).HasMaxLength(20);
        });

        modelBuilder.Entity<Event>(e =>
        {
            e.ToTable(table => table.HasCheckConstraint(
                "CK_Events_UrlAlias_Lowercase",
                "\"UrlAlias\" IS NULL OR \"UrlAlias\" = lower(\"UrlAlias\")"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.UrlAlias).HasMaxLength(64);
            e.HasIndex(x => x.UrlAlias).IsUnique();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.TieBreakMode).HasConversion<int>();
            e.Property(x => x.AllowTrialRuns).HasDefaultValue(true);
            // At most one featured event site-wide — DB-enforced so a future code
            // path can't break the invariant, not just FeatureEvent's own
            // unfeature-the-others logic.
            e.HasIndex(x => x.IsFeatured)
                .IsUnique()
                .HasFilter("\"IsFeatured\"")
                .HasDatabaseName("IX_Events_FeaturedEvent");
            // ListEvents' page query orders by CreatedAt DESC; a descending index
            // lets Postgres serve it directly instead of sorting after a scan.
            e.HasIndex(x => x.CreatedAt)
                .IsDescending()
                .HasDatabaseName("IX_Events_CreatedAt");
            // Trigram GIN indexes so ListEvents' ILike search is a bitmap index scan
            // rather than a sequential one — the standard Postgres answer for
            // ILIKE '%x%'. Requires the pg_trgm extension, created in the same migration.
            e.HasIndex(x => x.Name)
                .HasDatabaseName("IX_Events_Name_Trgm")
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");
            e.HasIndex(x => x.Description)
                .HasDatabaseName("IX_Events_Description_Trgm")
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");
            e.HasQueryFilter(x => !x.IsArchived);
            e.HasOne(x => x.CreatedBy)
                .WithMany()
                .HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);
            // Optimistic concurrency via Postgres's free xmin system column —
            // protects full-replace writes (e.g. PatchEvent) from silently
            // overwriting a concurrent change.
            e.Property<uint>("xmin").IsRowVersion();
        });

        modelBuilder.Entity<EventCompetitorModerator>(e =>
        {
            e.HasKey(x => new { x.EventId, x.CompetitorUserId, x.ModeratorUserId });
            e.HasOne(x => x.Competitor)
                .WithMany(x => x.Moderators)
                .HasForeignKey(x => new { x.EventId, x.CompetitorUserId })
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Moderator)
                .WithMany()
                .HasForeignKey(x => x.ModeratorUserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.ModeratorUserId);
            e.HasIndex(x => new { x.EventId, x.ModeratorUserId });
        });

        modelBuilder.Entity<EventCompetitor>(e =>
        {
            e.HasKey(x => new { x.EventId, x.UserId });
            e.HasOne(x => x.Event)
                .WithMany(x => x.Competitors)
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.EventId, x.IsStreamer });
        });

        modelBuilder.Entity<EventGame>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.EventId, x.KnownGameId })
                .HasFilter("\"KnownGameId\" IS NOT NULL");
            // At most one enabled ("active") game per event — DB-enforced so the
            // invariant can't be violated by a future code path, not just by
            // EnableEventGame's own disable-the-others logic.
            e.HasIndex(x => x.EventId)
                .IsUnique()
                .HasFilter("\"IsEnabled\"")
                .HasDatabaseName("IX_EventGames_EventId_ActiveGame");
            e.HasOne(x => x.Event)
                .WithMany(x => x.EventGames)
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.KnownGame)
                .WithMany()
                .HasForeignKey(x => x.KnownGameId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.CustomGameName).HasMaxLength(200);
            e.Property(x => x.CustomGameDescription).HasMaxLength(1000);
            e.Property<uint>("xmin").IsRowVersion();
        });

        modelBuilder.Entity<Objective>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Category).HasMaxLength(100);
            e.Property(x => x.Metadata).HasColumnType("jsonb");
            e.HasIndex(x => x.EventGameId);
            e.Property(x => x.Rule).HasColumnType("jsonb");
            e.Property(x => x.FailRule).HasColumnType("jsonb");
            e.HasOne(x => x.EventGame)
                .WithMany(x => x.Objectives)
                .HasForeignKey(x => x.EventGameId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Game)
                .WithMany(x => x.PredefinedObjectives)
                .HasForeignKey(x => x.GameId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property<uint>("xmin").IsRowVersion();
        });

        modelBuilder.Entity<CompletedObjective>(e =>
        {
            e.HasKey(x => x.Id);
            // At most one official completion per (objective, user), and separately
            // at most one per (objective, user) within a trial run. Two partial
            // indexes rather than one on (ObjectiveId, UserId, TrialRunId) because
            // Postgres treats NULLs as distinct, which would let a user record
            // multiple official (TrialRunId IS NULL) completions.
            e.HasIndex(x => new { x.ObjectiveId, x.UserId })
                .IsUnique()
                .HasFilter("\"TrialRunId\" IS NULL")
                .HasDatabaseName("IX_CompletedObjectives_ObjectiveId_UserId_Official");
            e.HasIndex(x => new { x.ObjectiveId, x.UserId, x.TrialRunId })
                .IsUnique()
                .HasFilter("\"TrialRunId\" IS NOT NULL")
                .HasDatabaseName("IX_CompletedObjectives_ObjectiveId_UserId_TrialRunId");
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.Objective)
                .WithMany(x => x.CompletedObjectives)
                .HasForeignKey(x => x.ObjectiveId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            // Disabling trial mode deletes the TrialRun row; every completion
            // recorded under it must disappear too: disabling deletes exactly
            // that competitor's trial rows for that event+game.
            e.HasOne(x => x.TrialRun)
                .WithMany(x => x.CompletedObjectives)
                .HasForeignKey(x => x.TrialRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FailedObjective>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ObjectiveId, x.UserId })
                .IsUnique()
                .HasFilter("\"TrialRunId\" IS NULL")
                .HasDatabaseName("IX_FailedObjectives_ObjectiveId_UserId_Official");
            e.HasIndex(x => new { x.ObjectiveId, x.UserId, x.TrialRunId })
                .IsUnique()
                .HasFilter("\"TrialRunId\" IS NOT NULL")
                .HasDatabaseName("IX_FailedObjectives_ObjectiveId_UserId_TrialRunId");
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.Objective)
                .WithMany(x => x.FailedObjectives)
                .HasForeignKey(x => x.ObjectiveId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.TrialRun)
                .WithMany(x => x.FailedObjectives)
                .HasForeignKey(x => x.TrialRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TrialRun>(e =>
        {
            e.HasKey(x => x.Id);
            // At most one trial "slot" per competitor per game — enabling
            // trial mode again while one already exists is a no-op, not a
            // second row.
            e.HasIndex(x => new { x.EventGameId, x.UserId }).IsUnique();
            e.Property(x => x.State).HasConversion<int>();
            e.HasOne(x => x.Event)
                .WithMany()
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.EventGame)
                .WithMany()
                .HasForeignKey(x => x.EventGameId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CalendarEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).IsRequired().HasMaxLength(120);
            e.Property(x => x.Color).HasConversion<int>();
            e.HasIndex(x => new { x.EventId, x.StartsAt });
            // Neither composite index leads with StartsAt, so the global calendar's
            // cross-event window scan can't use them.
            e.HasIndex(x => x.StartsAt).HasDatabaseName("IX_CalendarEntries_StartsAt");
            e.HasOne(x => x.Event)
                .WithMany()
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.CreatedBy)
                .WithMany()
                .HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);
            // Self-hosted, uploaded images only — never a URL.
            e.HasOne(x => x.ImageAsset)
                .WithMany()
                .HasForeignKey(x => x.ImageAssetId)
                .OnDelete(DeleteBehavior.Restrict);
            e.Property<uint>("xmin").IsRowVersion();
        });

        modelBuilder.Entity<PlannedRun>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Color).HasConversion<int>();
            e.HasIndex(x => new { x.EventId, x.StartsAt });
            e.HasIndex(x => new { x.UserId, x.StartsAt });
            e.HasIndex(x => x.StartsAt).HasDatabaseName("IX_PlannedRuns_StartsAt");
            e.HasOne(x => x.Event)
                .WithMany()
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.EventGame)
                .WithMany()
                .HasForeignKey(x => x.EventGameId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property<uint>("xmin").IsRowVersion();
        });

        modelBuilder.Entity<EventGameCompetitorInfo>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasConversion<int>();
            e.Property(x => x.Url).HasMaxLength(2048);
            e.Property(x => x.Text).HasMaxLength(2000);
            e.HasIndex(x => new { x.EventGameId, x.UserId });
            e.HasOne(x => x.EventGame)
                .WithMany(x => x.CompetitorInfos)
                .HasForeignKey(x => x.EventGameId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.CreatedBy)
                .WithMany()
                .HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);
            e.Property<uint>("xmin").IsRowVersion();
        });

        modelBuilder.Entity<TwitchExtensionSettings>(e =>
        {
            // Singleton, created lazily on the first admin save; Id is always 1.
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.DefaultScope).HasConversion<int>();
            e.HasOne(x => x.UpdatedBy)
                .WithMany()
                .HasForeignKey(x => x.UpdatedById)
                .OnDelete(DeleteBehavior.SetNull);
            e.Property<uint>("xmin").IsRowVersion();
        });

        modelBuilder.Entity<TwitchExtensionChannelSettings>(e =>
        {
            // Keyed on the Twitch channel id (a numeric string, well under 32
            // chars) rather than a surrogate: there is exactly one row per
            // channel and every lookup is by channel.
            e.HasKey(x => x.ChannelId);
            e.Property(x => x.ChannelId).HasMaxLength(32);
            e.Property(x => x.DefaultScope).HasConversion<int>();
            e.Property(x => x.HighlightChannelCompetitor).HasDefaultValue(true);
            e.Property(x => x.ShowTrialProgress).HasDefaultValue(true);
            // The push notifier looks up every channel showing a given event.
            e.HasIndex(x => x.EventId);
            // A deleted event or game must not take the channel's row with it:
            // the row falls back to the featured event instead.
            e.HasOne(x => x.Event)
                .WithMany()
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.PinnedEventGame)
                .WithMany()
                .HasForeignKey(x => x.PinnedEventGameId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.UpdatedBy)
                .WithMany()
                .HasForeignKey(x => x.UpdatedById)
                .OnDelete(DeleteBehavior.Restrict);
            e.Property<uint>("xmin").IsRowVersion();
        });

        modelBuilder.Entity<EventOverlayToken>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
            e.Property(x => x.TokenHash).IsRequired();
            e.Property(x => x.TokenPrefix).IsRequired().HasMaxLength(16);
            e.Property(x => x.SettingsJson).HasColumnType("jsonb");
            e.HasIndex(x => new { x.EventId, x.TokenPrefix });
            e.HasOne(x => x.Event)
                .WithMany()
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.CreatedBy)
                .WithMany()
                .HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).IsRequired().HasMaxLength(80);
            e.Property(x => x.Reason).HasMaxLength(2000);
            e.Property(x => x.BeforeJson).HasColumnType("jsonb");
            e.Property(x => x.AfterJson).HasColumnType("jsonb");

            // Id is an explicit trailing column so the keyset-pagination tuple
            // comparison (CreatedAt, Id) < (@c, @i) is satisfied from the index
            // alone, matching AuditsEndpoint.PaginateAsync's ordering.
            e.HasIndex(x => new { x.EventId, x.CreatedAt, x.Id })
                .HasDatabaseName("IX_AuditLogs_EventId_CreatedAt");
            e.HasIndex(x => new { x.CreatedAt, x.Id })
                .HasDatabaseName("IX_AuditLogs_CreatedAt");
            e.HasIndex(x => x.Type);
            e.HasIndex(x => x.ActorUserId);
            e.HasIndex(x => x.SubjectUserId);
            e.HasIndex(x => x.EventGameId);
            e.HasIndex(x => x.ObjectiveId);

            // Audits outlive every other row: keep them even if the actor,
            // subject or referenced event/game/objective is deleted.
            e.HasOne(x => x.Event)
                .WithMany()
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Actor)
                .WithMany()
                .HasForeignKey(x => x.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Subject)
                .WithMany()
                .HasForeignKey(x => x.SubjectUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<FeatureFlag>(e =>
        {
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(100);
            e.HasData(new FeatureFlag
            {
                Key = FeatureFlagKeys.MyEventsQuickComplete,
                Enabled = false,
            });
        });

        modelBuilder.Entity<EventRules>(e =>
        {
            e.HasKey(x => x.EventId);
            e.HasOne(x => x.Event)
                .WithOne()
                .HasForeignKey<EventRules>(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property<uint>("xmin").IsRowVersion();
        });

        modelBuilder.Entity<LegalDocument>(e =>
        {
            e.HasKey(x => x.Kind);
            e.Property(x => x.Kind).HasConversion<int>();
            e.Property<uint>("xmin").IsRowVersion();
        });

        modelBuilder.Entity<SiteTheme>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.BackgroundTreatment).HasConversion<int>();
            e.Property(x => x.Font).HasConversion<int>();
            e.HasOne(x => x.BackgroundAsset)
                .WithMany()
                .HasForeignKey(x => x.BackgroundAssetId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasData(new SiteTheme
            {
                Id = 1,
                BackgroundAssetId = null,
                BackgroundTreatment = BackgroundTreatment.None,
                Font = SiteFont.SystemSansSerif,
                LightDefault = "#f5f5f5",
                LightAccent = "#6d28d9",
                LightDanger = "#b91c1c",
                LightInfo = "#0369a1",
                LightSuccess = "#15803d",
                LightHighlight = "#b45309",
                DarkDefault = "#1e1e1e",
                DarkAccent = "#c4b5fd",
                DarkDanger = "#f87171",
                DarkInfo = "#38bdf8",
                DarkSuccess = "#4ade80",
                DarkHighlight = "#fbbf24",
                UpdatedAt = new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc),
            });
            e.Property<uint>("xmin").IsRowVersion();
        });

        modelBuilder.Entity<MediaAsset>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Sha256).IsRequired().HasMaxLength(64);
            e.HasIndex(x => x.Sha256).IsUnique();
            e.Property(x => x.ContentType).IsRequired().HasMaxLength(64);
            e.HasOne(x => x.CreatedBy)
                .WithMany()
                .HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        SeedGames(modelBuilder);
    }

    private static void SeedGames(ModelBuilder modelBuilder)
    {
        var seedPath = Path.Combine(AppContext.BaseDirectory, "Infrastructure", "Data", "Seed", "games.json");
        if (!File.Exists(seedPath)) return;

        var json = File.ReadAllText(seedPath);
        var games = JsonSerializer.Deserialize<List<Game>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (games is { Count: > 0 })
        {
            modelBuilder.Entity<Game>().HasData(games);
        }
    }
}
