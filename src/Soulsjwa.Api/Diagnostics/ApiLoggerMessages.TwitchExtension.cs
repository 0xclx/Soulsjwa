namespace Soulsjwa.Api.Diagnostics;

public static partial class ApiLoggerMessages
{
    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.TwitchExtensionPushSent,
        EventName = DiagnosticsConfig.LogEventNames.TwitchExtensionPushSent,
        Level = LogLevel.Debug,
        Message = "Twitch extension push sent: {Kind} to {Target}")]
    public static partial void TwitchExtensionPushSent(this ILogger logger, string kind, string target);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.TwitchExtensionPushRejected,
        EventName = DiagnosticsConfig.LogEventNames.TwitchExtensionPushRejected,
        Level = LogLevel.Warning,
        Message = "Twitch rejected an extension push ({Kind} to {Target}): {StatusCode}")]
    public static partial void TwitchExtensionPushRejected(this ILogger logger, string kind, string target, System.Net.HttpStatusCode statusCode);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.TwitchExtensionPushFailed,
        EventName = DiagnosticsConfig.LogEventNames.TwitchExtensionPushFailed,
        Level = LogLevel.Warning,
        Message = "Twitch extension push failed ({Kind} to {Target}); viewers fall back to polling")]
    public static partial void TwitchExtensionPushFailed(this ILogger logger, Exception exception, string kind, string target);
}
