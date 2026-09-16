namespace Soulsjwa.Api.Diagnostics;

public static partial class ApiLoggerMessages
{
    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.AdminUserRoleSet,
        EventName = DiagnosticsConfig.LogEventNames.AdminUserRoleSet,
        Level = LogLevel.Information,
        Message = "Set user {TargetUserId} role to {Role} by admin {UserId}")]
    public static partial void AdminUserRoleSet(this ILogger logger, Guid targetUserId, string role, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.AllowlistLoginAdded,
        EventName = DiagnosticsConfig.LogEventNames.AllowlistLoginAdded,
        Level = LogLevel.Information,
        Message = "Added Twitch login {TwitchLogin} to allowlist by user {UserId}; linked user {LinkedUserId}")]
    public static partial void AllowlistLoginAdded(this ILogger logger, string twitchLogin, Guid userId, Guid? linkedUserId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.AllowlistLoginRemoved,
        EventName = DiagnosticsConfig.LogEventNames.AllowlistLoginRemoved,
        Level = LogLevel.Information,
        Message = "Removed Twitch login {TwitchLogin} from allowlist by user {UserId}; linked user {LinkedUserId}")]
    public static partial void AllowlistLoginRemoved(this ILogger logger, string twitchLogin, Guid userId, Guid? linkedUserId);
}
