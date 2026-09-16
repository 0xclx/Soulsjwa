namespace Soulsjwa.Api.Diagnostics;

public static partial class ApiLoggerMessages
{
    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.RetentionPassCompleted,
        EventName = DiagnosticsConfig.LogEventNames.RetentionPassCompleted,
        Level = LogLevel.Information,
        Message = "Retention pass completed in {DurationMs}ms: {RefreshTokensDeleted} refresh tokens, {AuditLogsDeleted} audit logs deleted")]
    public static partial void RetentionPassCompleted(this ILogger logger, long durationMs, int refreshTokensDeleted, int auditLogsDeleted);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.RetentionPassSkipped,
        EventName = DiagnosticsConfig.LogEventNames.RetentionPassSkipped,
        Level = LogLevel.Debug,
        Message = "Retention pass skipped: another instance holds the advisory lock")]
    public static partial void RetentionPassSkipped(this ILogger logger);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.RetentionPassFailed,
        EventName = DiagnosticsConfig.LogEventNames.RetentionPassFailed,
        Level = LogLevel.Error,
        Message = "Retention pass failed")]
    public static partial void RetentionPassFailed(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.CorrelationIdRejected,
        EventName = DiagnosticsConfig.LogEventNames.CorrelationIdRejected,
        Level = LogLevel.Debug,
        Message = "Rejected inbound X-Correlation-Id ({Length} chars): does not match the expected format")]
    public static partial void CorrelationIdRejected(this ILogger logger, int length);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.MediaFileMissing,
        EventName = DiagnosticsConfig.LogEventNames.MediaFileMissing,
        Level = LogLevel.Warning,
        Message = "MediaAsset {AssetId} (sha256 {Sha256}) has a database row but no file on disk")]
    public static partial void MediaFileMissing(this ILogger logger, Guid assetId, string sha256);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.RateLimitRejected,
        EventName = DiagnosticsConfig.LogEventNames.RateLimitRejected,
        Level = LogLevel.Debug,
        Message = "Rate limit rejected {Caller} on {Method} {Path} (policy: {Policy})")]
    public static partial void RateLimitRejected(this ILogger logger, string caller, string method, string path, string policy);
}
