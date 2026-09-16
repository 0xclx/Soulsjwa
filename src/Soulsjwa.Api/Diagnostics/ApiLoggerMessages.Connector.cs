namespace Soulsjwa.Api.Diagnostics;

public static partial class ApiLoggerMessages
{
    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.ConnectorSubmissionProcessed,
        EventName = DiagnosticsConfig.LogEventNames.ConnectorSubmissionProcessed,
        Level = LogLevel.Information,
        Message = "Processed connector submission for event {EventId}, event game {EventGameId}, user {UserId}: {CompletedCount} objectives completed, {FailedCount} objectives failed")]
    public static partial void ConnectorSubmissionProcessed(
        this ILogger logger,
        Guid eventId,
        Guid eventGameId,
        Guid userId,
        int completedCount,
        int failedCount);
}
