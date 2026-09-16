namespace Soulsjwa.Api.Diagnostics;

public static partial class ApiLoggerMessages
{
    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.EventCreated,
        EventName = DiagnosticsConfig.LogEventNames.EventCreated,
        Level = LogLevel.Information,
        Message = "Created event {EventId} by user {UserId}")]
    public static partial void EventCreated(this ILogger logger, Guid eventId, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.EventUpdated,
        EventName = DiagnosticsConfig.LogEventNames.EventUpdated,
        Level = LogLevel.Information,
        Message = "Updated event {EventId} by user {UserId}")]
    public static partial void EventUpdated(this ILogger logger, Guid eventId, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.EventArchived,
        EventName = DiagnosticsConfig.LogEventNames.EventArchived,
        Level = LogLevel.Information,
        Message = "Archived event {EventId} by user {UserId}")]
    public static partial void EventArchived(this ILogger logger, Guid eventId, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.EventUnarchived,
        EventName = DiagnosticsConfig.LogEventNames.EventUnarchived,
        Level = LogLevel.Information,
        Message = "Unarchived event {EventId} by user {UserId}")]
    public static partial void EventUnarchived(this ILogger logger, Guid eventId, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.EventStarted,
        EventName = DiagnosticsConfig.LogEventNames.EventStarted,
        Level = LogLevel.Information,
        Message = "Started event {EventId} by user {UserId}")]
    public static partial void EventStarted(this ILogger logger, Guid eventId, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.EventStopped,
        EventName = DiagnosticsConfig.LogEventNames.EventStopped,
        Level = LogLevel.Information,
        Message = "Stopped event {EventId} by user {UserId}")]
    public static partial void EventStopped(this ILogger logger, Guid eventId, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.EventFeatured,
        EventName = DiagnosticsConfig.LogEventNames.EventFeatured,
        Level = LogLevel.Information,
        Message = "Featured event {EventId} by user {UserId}")]
    public static partial void EventFeatured(this ILogger logger, Guid eventId, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.EventUnfeatured,
        EventName = DiagnosticsConfig.LogEventNames.EventUnfeatured,
        Level = LogLevel.Information,
        Message = "Unfeatured event {EventId} by user {UserId}")]
    public static partial void EventUnfeatured(this ILogger logger, Guid eventId, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.PredefinedObjectiveCreated,
        EventName = DiagnosticsConfig.LogEventNames.PredefinedObjectiveCreated,
        Level = LogLevel.Information,
        Message = "Created predefined objective {ObjectiveId}")]
    public static partial void PredefinedObjectiveCreated(this ILogger logger, Guid objectiveId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.ObjectiveCreated,
        EventName = DiagnosticsConfig.LogEventNames.ObjectiveCreated,
        Level = LogLevel.Information,
        Message = "Created objective {ObjectiveId} for event game {EventGameId} in event {EventId} by user {UserId}")]
    public static partial void ObjectiveCreated(this ILogger logger, Guid objectiveId, Guid eventGameId, Guid eventId, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.PredefinedObjectiveAssigned,
        EventName = DiagnosticsConfig.LogEventNames.PredefinedObjectiveAssigned,
        Level = LogLevel.Information,
        Message = "Assigned predefined objective {PredefinedObjectiveId} as objective {ObjectiveId} to event game {EventGameId} in event {EventId} by user {UserId}")]
    public static partial void PredefinedObjectiveAssigned(
        this ILogger logger,
        Guid predefinedObjectiveId,
        Guid objectiveId,
        Guid eventGameId,
        Guid eventId,
        Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.ObjectiveUpdated,
        EventName = DiagnosticsConfig.LogEventNames.ObjectiveUpdated,
        Level = LogLevel.Information,
        Message = "Updated objective {ObjectiveId} for event game {EventGameId} in event {EventId} by user {UserId}")]
    public static partial void ObjectiveUpdated(this ILogger logger, Guid objectiveId, Guid eventGameId, Guid eventId, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.ObjectiveDeleted,
        EventName = DiagnosticsConfig.LogEventNames.ObjectiveDeleted,
        Level = LogLevel.Information,
        Message = "Deleted objective {ObjectiveId} for event game {EventGameId} in event {EventId} by user {UserId}")]
    public static partial void ObjectiveDeleted(this ILogger logger, Guid objectiveId, Guid eventGameId, Guid eventId, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.PredefinedObjectivesImported,
        EventName = DiagnosticsConfig.LogEventNames.PredefinedObjectivesImported,
        Level = LogLevel.Information,
        Message = "Imported {ImportedCount} predefined objectives and skipped {SkippedCount} for event game {EventGameId} in event {EventId} by user {UserId}")]
    public static partial void PredefinedObjectivesImported(
        this ILogger logger,
        int importedCount,
        int skippedCount,
        Guid eventGameId,
        Guid eventId,
        Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.CompetitorAdded,
        EventName = DiagnosticsConfig.LogEventNames.CompetitorAdded,
        Level = LogLevel.Information,
        Message = "Added competitor {CompetitorUserId} to event {EventId} by user {UserId}")]
    public static partial void CompetitorAdded(this ILogger logger, Guid competitorUserId, Guid eventId, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.CompetitorRemoved,
        EventName = DiagnosticsConfig.LogEventNames.CompetitorRemoved,
        Level = LogLevel.Information,
        Message = "Removed competitor {CompetitorUserId} from event {EventId} by user {UserId}")]
    public static partial void CompetitorRemoved(this ILogger logger, Guid competitorUserId, Guid eventId, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.EventGameAdded,
        EventName = DiagnosticsConfig.LogEventNames.EventGameAdded,
        Level = LogLevel.Information,
        Message = "Added game {GameId} as event game {EventGameId} to event {EventId} by user {UserId}")]
    public static partial void EventGameAdded(this ILogger logger, int gameId, Guid eventGameId, Guid eventId, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.CustomEventGameAdded,
        EventName = DiagnosticsConfig.LogEventNames.CustomEventGameAdded,
        Level = LogLevel.Information,
        Message = "Added custom event game {EventGameId} to event {EventId} by user {UserId}")]
    public static partial void CustomEventGameAdded(this ILogger logger, Guid eventGameId, Guid eventId, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.EventGameRemoved,
        EventName = DiagnosticsConfig.LogEventNames.EventGameRemoved,
        Level = LogLevel.Information,
        Message = "Removed event game {EventGameId} from event {EventId} by user {UserId}")]
    public static partial void EventGameRemoved(this ILogger logger, Guid eventGameId, Guid eventId, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.EventGameEnabled,
        EventName = DiagnosticsConfig.LogEventNames.EventGameEnabled,
        Level = LogLevel.Information,
        Message = "Enabled event game {EventGameId} in event {EventId} by user {UserId}")]
    public static partial void EventGameEnabled(this ILogger logger, Guid eventGameId, Guid eventId, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.EventGameDisabled,
        EventName = DiagnosticsConfig.LogEventNames.EventGameDisabled,
        Level = LogLevel.Information,
        Message = "Disabled event game {EventGameId} in event {EventId} by user {UserId}")]
    public static partial void EventGameDisabled(this ILogger logger, Guid eventGameId, Guid eventId, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.EventGameUpdated,
        EventName = DiagnosticsConfig.LogEventNames.EventGameUpdated,
        Level = LogLevel.Information,
        Message = "Updated event game {EventGameId} in event {EventId} by user {UserId}")]
    public static partial void EventGameUpdated(this ILogger logger, Guid eventGameId, Guid eventId, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.EventGamesReordered,
        EventName = DiagnosticsConfig.LogEventNames.EventGamesReordered,
        Level = LogLevel.Information,
        Message = "Reordered games in event {EventId} by user {UserId}")]
    public static partial void EventGamesReordered(this ILogger logger, Guid eventId, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.ObjectivesReordered,
        EventName = DiagnosticsConfig.LogEventNames.ObjectivesReordered,
        Level = LogLevel.Information,
        Message = "Reordered objectives for event game {EventGameId} in event {EventId} by user {UserId}")]
    public static partial void ObjectivesReordered(this ILogger logger, Guid eventGameId, Guid eventId, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.StreamerAdded,
        EventName = DiagnosticsConfig.LogEventNames.StreamerAdded,
        Level = LogLevel.Information,
        Message = "Marked competitor {StreamerUserId} as streamer in event {EventId} by user {UserId}")]
    public static partial void StreamerAdded(this ILogger logger, Guid streamerUserId, Guid eventId, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.StreamerRemoved,
        EventName = DiagnosticsConfig.LogEventNames.StreamerRemoved,
        Level = LogLevel.Information,
        Message = "Unmarked competitor {StreamerUserId} as streamer in event {EventId} by user {UserId}")]
    public static partial void StreamerRemoved(this ILogger logger, Guid streamerUserId, Guid eventId, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.ModeratorAdded,
        EventName = DiagnosticsConfig.LogEventNames.ModeratorAdded,
        Level = LogLevel.Information,
        Message = "Added moderator {ModeratorUserId} for streamer competitor {StreamerUserId} in event {EventId} by user {UserId}")]
    public static partial void ModeratorAdded(this ILogger logger, Guid moderatorUserId, Guid streamerUserId, Guid eventId, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.ModeratorRemoved,
        EventName = DiagnosticsConfig.LogEventNames.ModeratorRemoved,
        Level = LogLevel.Information,
        Message = "Removed moderator {ModeratorUserId} for streamer competitor {StreamerUserId} in event {EventId} by user {UserId}")]
    public static partial void ModeratorRemoved(this ILogger logger, Guid moderatorUserId, Guid streamerUserId, Guid eventId, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.ObjectiveCompleted,
        EventName = DiagnosticsConfig.LogEventNames.ObjectiveCompleted,
        Level = LogLevel.Information,
        Message = "Completed objective {ObjectiveId} for user {TargetUserId} in event {EventId} by user {UserId}")]
    public static partial void ObjectiveCompleted(this ILogger logger, Guid objectiveId, Guid targetUserId, Guid eventId, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.ObjectiveUncompleted,
        EventName = DiagnosticsConfig.LogEventNames.ObjectiveUncompleted,
        Level = LogLevel.Information,
        Message = "Uncompleted objective {ObjectiveId} for user {TargetUserId} in event {EventId} by user {UserId}")]
    public static partial void ObjectiveUncompleted(this ILogger logger, Guid objectiveId, Guid targetUserId, Guid eventId, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.ObjectiveCompletionTimeEdited,
        EventName = DiagnosticsConfig.LogEventNames.ObjectiveCompletionTimeEdited,
        Level = LogLevel.Information,
        Message = "Edited completion time of objective {ObjectiveId} for user {TargetUserId} in event {EventId} by user {UserId}")]
    public static partial void ObjectiveCompletionTimeEdited(this ILogger logger, Guid objectiveId, Guid targetUserId, Guid eventId, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.ObjectiveFailed,
        EventName = DiagnosticsConfig.LogEventNames.ObjectiveFailed,
        Level = LogLevel.Information,
        Message = "Failed objective {ObjectiveId} for user {TargetUserId} in event {EventId} by user {UserId}")]
    public static partial void ObjectiveFailed(this ILogger logger, Guid objectiveId, Guid targetUserId, Guid eventId, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.ObjectiveUnfailed,
        EventName = DiagnosticsConfig.LogEventNames.ObjectiveUnfailed,
        Level = LogLevel.Information,
        Message = "Reset (unfailed) objective {ObjectiveId} for user {TargetUserId} in event {EventId} by user {UserId}")]
    public static partial void ObjectiveUnfailed(this ILogger logger, Guid objectiveId, Guid targetUserId, Guid eventId, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.ObjectivesRemainingFailed,
        EventName = DiagnosticsConfig.LogEventNames.ObjectivesRemainingFailed,
        Level = LogLevel.Information,
        Message = "Failed the {FailedCount} remaining objectives of event game {EventGameId} for user {TargetUserId} in event {EventId} by user {UserId}")]
    public static partial void ObjectivesRemainingFailed(this ILogger logger, int failedCount, Guid eventGameId, Guid targetUserId, Guid eventId, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.CompetitorInfoAdded,
        EventName = DiagnosticsConfig.LogEventNames.CompetitorInfoAdded,
        Level = LogLevel.Information,
        Message = "Added competitor info {InfoId} for user {TargetUserId} on event game {EventGameId} in event {EventId} by user {UserId}")]
    public static partial void CompetitorInfoAdded(this ILogger logger, Guid infoId, Guid targetUserId, Guid eventGameId, Guid eventId, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.CompetitorInfoUpdated,
        EventName = DiagnosticsConfig.LogEventNames.CompetitorInfoUpdated,
        Level = LogLevel.Information,
        Message = "Updated competitor info {InfoId} for user {TargetUserId} on event game {EventGameId} in event {EventId} by user {UserId}")]
    public static partial void CompetitorInfoUpdated(this ILogger logger, Guid infoId, Guid targetUserId, Guid eventGameId, Guid eventId, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.CompetitorInfoRemoved,
        EventName = DiagnosticsConfig.LogEventNames.CompetitorInfoRemoved,
        Level = LogLevel.Information,
        Message = "Removed competitor info {InfoId} for user {TargetUserId} on event game {EventGameId} in event {EventId} by user {UserId}")]
    public static partial void CompetitorInfoRemoved(this ILogger logger, Guid infoId, Guid targetUserId, Guid eventGameId, Guid eventId, Guid userId);
}
