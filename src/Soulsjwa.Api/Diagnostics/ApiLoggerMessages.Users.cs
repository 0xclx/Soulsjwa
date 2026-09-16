namespace Soulsjwa.Api.Diagnostics;

public static partial class ApiLoggerMessages
{
    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.ApiKeyCreated,
        EventName = DiagnosticsConfig.LogEventNames.ApiKeyCreated,
        Level = LogLevel.Information,
        Message = "Created API key {ApiKeyId} with prefix {ApiKeyPrefix} for user {UserId}")]
    public static partial void ApiKeyCreated(this ILogger logger, Guid apiKeyId, string apiKeyPrefix, Guid userId);

    [LoggerMessage(
        EventId = DiagnosticsConfig.LogEventIds.ApiKeyRevoked,
        EventName = DiagnosticsConfig.LogEventNames.ApiKeyRevoked,
        Level = LogLevel.Information,
        Message = "Revoked API key {ApiKeyId} with prefix {ApiKeyPrefix} for user {UserId}")]
    public static partial void ApiKeyRevoked(this ILogger logger, Guid apiKeyId, string apiKeyPrefix, Guid userId);
}
