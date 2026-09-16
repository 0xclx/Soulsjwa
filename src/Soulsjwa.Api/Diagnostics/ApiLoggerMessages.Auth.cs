namespace Soulsjwa.Api.Diagnostics;

public static partial class ApiLoggerMessages
{
    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.OAuthStateMismatch,
        EventName = DiagnosticsConfig.LogEventNames.OAuthStateMismatch,
        Level = LogLevel.Warning,
        Message = "OAuth state mismatch")]
    public static partial void OAuthStateMismatch(this ILogger logger);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.TwitchAuthorizationUrlGenerated,
        EventName = DiagnosticsConfig.LogEventNames.TwitchAuthorizationUrlGenerated,
        Level = LogLevel.Debug,
        Message = "Generated Twitch authorization URL with scope {Scope}")]
    public static partial void TwitchAuthorizationUrlGenerated(this ILogger logger, string scope);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.TwitchCodeExchangeFailed,
        EventName = DiagnosticsConfig.LogEventNames.TwitchCodeExchangeFailed,
        Level = LogLevel.Error,
        Message = "Failed to exchange Twitch code: {StatusCode}")]
    public static partial void TwitchCodeExchangeFailed(this ILogger logger, System.Net.HttpStatusCode statusCode);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.TwitchCodeExchanged,
        EventName = DiagnosticsConfig.LogEventNames.TwitchCodeExchanged,
        Level = LogLevel.Information,
        Message = "Exchanged Twitch OAuth code successfully")]
    public static partial void TwitchCodeExchanged(this ILogger logger);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.TwitchUserInfoFailed,
        EventName = DiagnosticsConfig.LogEventNames.TwitchUserInfoFailed,
        Level = LogLevel.Error,
        Message = "Failed to get Twitch user info: {StatusCode}")]
    public static partial void TwitchUserInfoFailed(this ILogger logger, System.Net.HttpStatusCode statusCode);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.TwitchUserInfoEmpty,
        EventName = DiagnosticsConfig.LogEventNames.TwitchUserInfoEmpty,
        Level = LogLevel.Warning,
        Message = "Twitch user info response contained no users")]
    public static partial void TwitchUserInfoEmpty(this ILogger logger);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.TwitchUserInfoMalformed,
        EventName = DiagnosticsConfig.LogEventNames.TwitchUserInfoMalformed,
        Level = LogLevel.Error,
        Message = "Twitch user info response was oversized or not valid JSON")]
    public static partial void TwitchUserInfoMalformed(this ILogger logger);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.TwitchUserInfoRetrieved,
        EventName = DiagnosticsConfig.LogEventNames.TwitchUserInfoRetrieved,
        Level = LogLevel.Debug,
        Message = "Retrieved Twitch user info for login {TwitchLogin} ({TwitchUserId})")]
    public static partial void TwitchUserInfoRetrieved(this ILogger logger, string twitchLogin, string twitchUserId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.TwitchLoginRejected,
        EventName = DiagnosticsConfig.LogEventNames.TwitchLoginRejected,
        Level = LogLevel.Warning,
        Message = "Rejected Twitch login {TwitchLogin} ({TwitchUserId}) - not on allowlist")]
    public static partial void TwitchLoginRejected(this ILogger logger, string twitchLogin, string twitchUserId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.TwitchUserUpserted,
        EventName = DiagnosticsConfig.LogEventNames.TwitchUserUpserted,
        Level = LogLevel.Information,
        Message = "User operation completed: {Action} user {UserId} from Twitch login {TwitchLogin} with role {Role}")]
    public static partial void TwitchUserUpserted(
        this ILogger logger,
        string action,
        Guid userId,
        string twitchLogin,
        string role);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.TwitchUserUpsertFailed,
        EventName = DiagnosticsConfig.LogEventNames.TwitchUserUpsertFailed,
        Level = LogLevel.Error,
        Message = "Failed to upsert Twitch user {TwitchLogin} ({TwitchUserId})")]
    public static partial void TwitchUserUpsertFailed(
        this ILogger logger,
        Exception exception,
        string twitchLogin,
        string twitchUserId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.JwtAccessTokenGenerated,
        EventName = DiagnosticsConfig.LogEventNames.JwtAccessTokenGenerated,
        Level = LogLevel.Debug,
        Message = "Generated access token for user {UserId} with role {Role}")]
    public static partial void JwtAccessTokenGenerated(this ILogger logger, Guid userId, string role);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.JwtRefreshTokenGenerated,
        EventName = DiagnosticsConfig.LogEventNames.JwtRefreshTokenGenerated,
        Level = LogLevel.Debug,
        Message = "Generated refresh token for user {UserId} with token prefix {Prefix}")]
    public static partial void JwtRefreshTokenGenerated(this ILogger logger, Guid userId, string prefix);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.JwtRefreshTokenGenerationFailed,
        EventName = DiagnosticsConfig.LogEventNames.JwtRefreshTokenGenerationFailed,
        Level = LogLevel.Error,
        Message = "Failed to generate refresh token for user {UserId}")]
    public static partial void JwtRefreshTokenGenerationFailed(this ILogger logger, Exception exception, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.JwtRefreshTokenUnknown,
        EventName = DiagnosticsConfig.LogEventNames.JwtRefreshTokenUnknown,
        Level = LogLevel.Warning,
        Message = "Refresh token validation failed for unknown token prefix {Prefix}")]
    public static partial void JwtRefreshTokenUnknown(this ILogger logger, string prefix);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.JwtRefreshTokenReuseDetected,
        EventName = DiagnosticsConfig.LogEventNames.JwtRefreshTokenReuseDetected,
        Level = LogLevel.Warning,
        Message = "Refresh-token reuse detected for user {UserId} (token prefix {Prefix}); revoking entire family.")]
    public static partial void JwtRefreshTokenReuseDetected(this ILogger logger, Guid userId, string prefix);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.JwtInactiveRefreshTokenRejected,
        EventName = DiagnosticsConfig.LogEventNames.JwtInactiveRefreshTokenRejected,
        Level = LogLevel.Warning,
        Message = "Inactive refresh token rejected for user {UserId} with token prefix {Prefix}")]
    public static partial void JwtInactiveRefreshTokenRejected(this ILogger logger, Guid userId, string prefix);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.JwtRefreshTokensRevoked,
        EventName = DiagnosticsConfig.LogEventNames.JwtRefreshTokensRevoked,
        Level = LogLevel.Information,
        Message = "Revoked {TokenCount} refresh tokens for user {UserId}")]
    public static partial void JwtRefreshTokensRevoked(this ILogger logger, int tokenCount, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.JwtRefreshTokenRevoked,
        EventName = DiagnosticsConfig.LogEventNames.JwtRefreshTokenRevoked,
        Level = LogLevel.Information,
        Message = "Revoked refresh token for user {UserId} with token prefix {Prefix}")]
    public static partial void JwtRefreshTokenRevoked(this ILogger logger, Guid userId, string prefix);
}
