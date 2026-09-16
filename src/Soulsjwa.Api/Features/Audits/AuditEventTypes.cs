namespace Soulsjwa.Api.Features.Audits;

/// <summary>
/// Canonical action types stored in <see cref="Entities.AuditLog.Type"/>. String
/// constants rather than an enum so a new type needs no EF migration, and rows
/// referring to removed/renamed types still render in the UI as-is.
/// </summary>
public static class AuditEventTypes
{
    // Event lifecycle
    public const string EventCreated = "event.created";
    public const string EventUpdated = "event.updated";
    public const string EventArchived = "event.archived";
    public const string EventUnarchived = "event.unarchived";
    public const string EventStarted = "event.started";
    public const string EventStopped = "event.stopped";
    public const string EventFeatured = "event.featured";
    public const string EventUnfeatured = "event.unfeatured";
    public const string EventDuplicated = "event.duplicated";

    // Event games
    public const string EventGameAdded = "event_game.added";
    public const string EventGameRemoved = "event_game.removed";
    public const string EventGameEnabled = "event_game.enabled";
    public const string EventGameDisabled = "event_game.disabled";
    public const string EventGameUpdated = "event_game.updated";
    public const string EventGamesReordered = "event_game.reordered";

    // Objectives
    public const string ObjectiveCreated = "objective.created";
    public const string ObjectiveUpdated = "objective.updated";
    public const string ObjectiveDeleted = "objective.deleted";
    public const string ObjectivePredefinedCreated = "objective.predefined_created";
    public const string ObjectivePredefinedAssigned = "objective.predefined_assigned";
    public const string ObjectivePredefinedImported = "objective.predefined_imported";
    public const string ObjectivesReordered = "objective.reordered";

    // Completed objectives
    public const string ObjectiveCompleted = "objective.completed";
    public const string ObjectiveUncompleted = "objective.uncompleted";
    public const string ObjectiveCompletionTimeEdited = "objective.completion_time_edited";

    // Failed objectives
    public const string ObjectiveFailed = "objective.failed";
    public const string ObjectiveUnfailed = "objective.unfailed";
    public const string ObjectivesRemainingFailed = "objective.remaining_failed";

    // Competitors
    public const string CompetitorAdded = "competitor.added";
    public const string CompetitorRemoved = "competitor.removed";

    // Streamers & moderators
    public const string StreamerLiveChanged = "streamer.live_changed";
    public const string StreamerAdded = "streamer.added";
    public const string StreamerRemoved = "streamer.removed";
    public const string ModeratorAdded = "moderator.added";
    public const string ModeratorRemoved = "moderator.removed";

    // Competitor info
    public const string CompetitorInfoAdded = "competitor_info.added";
    public const string CompetitorInfoUpdated = "competitor_info.updated";
    public const string CompetitorInfoRemoved = "competitor_info.removed";

    // Overlay tokens
    public const string OverlayTokenCreated = "overlay_token.created";
    public const string OverlayTokenRevoked = "overlay_token.revoked";
    public const string OverlayTokenSettingsUpdated = "overlay_token.settings_updated";

    // Twitch extension
    public const string TwitchExtensionConfigurationUpdated = "twitch_extension.configuration_updated";
    public const string TwitchExtensionSettingsUpdated = "twitch_extension.settings_updated";

    // Admin / global (no event scope)
    public const string UserRoleChanged = "user.role_changed";
    public const string AllowlistAdded = "allowlist.added";
    public const string AllowlistRemoved = "allowlist.removed";
    public const string FeatureFlagUpdated = "feature_flag.updated";

    // Media
    public const string MediaUploaded = "media.uploaded";

    // Trial runs
    public const string TrialRunEnabled = "trial_run.enabled";
    public const string TrialRunDisabled = "trial_run.disabled";
    public const string TrialRunStarted = "trial_run.started";
    public const string TrialRunStopped = "trial_run.stopped";
    public const string TrialRunReset = "trial_run.reset";

    // Event rules
    public const string EventRulesUpdated = "event_rules.updated";

    // Legal documents
    public const string LegalDocumentUpdated = "legal_document.updated";

    // Site theme
    public const string SiteThemeUpdated = "site_theme.updated";

    // Calendar entries
    public const string CalendarEntryCreated = "calendar_entry.created";
    public const string CalendarEntryUpdated = "calendar_entry.updated";
    public const string CalendarEntryDeleted = "calendar_entry.deleted";

    // Planned runs
    public const string PlannedRunCreated = "planned_run.created";
    public const string PlannedRunUpdated = "planned_run.updated";
    public const string PlannedRunDeleted = "planned_run.deleted";
}
