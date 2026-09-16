using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Soulsjwa.Api.Diagnostics;

public static class DiagnosticsConfig
{
    public const string ServiceName = "Soulsjwa.Api";

    public static readonly ActivitySource ActivitySource = new(ServiceName);
    public static readonly Meter Meter = new(ServiceName);

    public static class ActivityNames
    {
        public const string TwitchAuthorizationUrl = "TwitchAuthService.GetAuthorizationUrl";
        public const string TwitchExchangeCode = "TwitchAuthService.ExchangeCode";
        public const string TwitchGetUserInfo = "TwitchAuthService.GetUserInfo";
        public const string TwitchUpsertUser = "TwitchAuthService.UpsertUser";
        public const string SeedPredefinedObjectives = "PredefinedObjectiveSeeder.Seed";
        public const string ConnectorSubmitGameData = "ConnectorEndpoint.SubmitGameData";
        public const string JwtGenerateAccessToken = "JwtTokenService.GenerateAccessToken";
        public const string JwtGenerateRefreshToken = "JwtTokenService.GenerateRefreshToken";
        public const string JwtValidateRefreshToken = "JwtTokenService.ValidateRefreshToken";
        public const string JwtRevokeAllForUser = "JwtTokenService.RevokeAllForUser";
        public const string JwtRevokeRefreshToken = "JwtTokenService.RevokeRefreshToken";
    }

    public static class BusinessOperationNames
    {
        public const string TwitchAuthorizationUrl = "twitch_authorization_url";
        public const string TwitchExchangeCode = "twitch_exchange_code";
        public const string TwitchGetUserInfo = "twitch_get_user_info";
        public const string TwitchUpsertUser = "twitch_upsert_user";
        public const string SeedPredefinedObjectives = "seed_predefined_objectives";
        public const string ConnectorSubmitGameData = "connector_submit_game_data";
        public const string JwtGenerateAccessToken = "generate_access_token";
        public const string JwtGenerateRefreshToken = "generate_refresh_token";
        public const string JwtValidateRefreshToken = "validate_refresh_token";
        public const string JwtRevokeAllRefreshTokens = "revoke_all_refresh_tokens";
        public const string JwtRevokeRefreshToken = "revoke_refresh_token";
    }

    public static class OperationStatuses
    {
        public const string Success = "success";
        public const string Failure = "failure";
        public const string Empty = "empty";
        public const string NotAllowlisted = "not_allowlisted";
        public const string NotFound = "not_found";
        public const string ReuseDetected = "reuse_detected";
        public const string Inactive = "inactive";
        public const string InvalidJson = "invalid_json";
        public const string InvalidOperation = "invalid_operation";
        public const string InvalidArgument = "invalid_argument";
    }

    public static class Tags
    {
        public const string Method = "method";
        public const string Route = "route";
        public const string StatusCode = "status_code";
        public const string Operation = "operation";
        public const string OperationType = "operation.type";
        public const string Status = "status";
        public const string OAuthProvider = "oauth.provider";
        public const string TwitchUserId = "twitch.user_id";
        public const string TwitchLogin = "twitch.login";
        public const string UserId = "user.id";
        public const string UserRole = "user.role";
        public const string UserCreated = "user.created";
        public const string RefreshTokenId = "refresh_token.id";
        public const string RefreshTokensRevoked = "refresh_tokens.revoked";
        public const string RuleResult = "rule.result";
        public const string RulesEvaluated = "rules.evaluated";
        public const string ObjectivesDefined = "objectives.defined";
        public const string ObjectivesAdded = "objectives.added";
    }

    public static class OperationTypes
    {
        public const string Create = "create";
        public const string Update = "update";
    }

    public static class LogActions
    {
        public const string Created = "Created";
        public const string Updated = "Updated";
    }

    public static class LogEventIds
    {
        public const int TwitchAuthorizationUrlGenerated = 1000;
        public const int TwitchCodeExchangeFailed = 1001;
        public const int TwitchCodeExchanged = 1002;
        public const int TwitchUserInfoFailed = 1003;
        public const int TwitchUserInfoEmpty = 1004;
        public const int TwitchUserInfoRetrieved = 1005;
        public const int TwitchLoginRejected = 1006;
        public const int TwitchUserUpserted = 1007;
        public const int TwitchExtensionPushSent = 1008;
        public const int TwitchExtensionPushRejected = 1009;
        public const int TwitchExtensionPushFailed = 1010;
        public const int TwitchUserUpsertFailed = 1008;
        public const int OAuthStateMismatch = 1009;
        public const int TwitchUserInfoMalformed = 1010;
        public const int JwtAccessTokenGenerated = 1100;
        public const int JwtRefreshTokenGenerated = 1101;
        public const int JwtRefreshTokenGenerationFailed = 1102;
        public const int JwtRefreshTokenUnknown = 1103;
        public const int JwtRefreshTokenReuseDetected = 1104;
        public const int JwtInactiveRefreshTokenRejected = 1105;
        public const int JwtRefreshTokensRevoked = 1106;
        public const int JwtRefreshTokenRevoked = 1107;
        public const int PredefinedObjectivesSeeded = 2000;
        public const int AdminUserRoleSet = 3000;
        public const int AllowlistLoginAdded = 3001;
        public const int AllowlistLoginRemoved = 3002;
        public const int ApiKeyCreated = 4000;
        public const int ApiKeyRevoked = 4001;
        public const int ConnectorSubmissionProcessed = 5001;
        public const int EventCreated = 6000;
        public const int EventUpdated = 6001;
        public const int EventArchived = 6002;
        public const int EventUnarchived = 6005;
        public const int EventStarted = 6003;
        public const int EventStopped = 6004;
        public const int EventFeatured = 6006;
        public const int EventUnfeatured = 6007;
        public const int PredefinedObjectiveCreated = 6010;
        public const int ObjectiveCreated = 6011;
        public const int PredefinedObjectiveAssigned = 6012;
        public const int ObjectiveUpdated = 6013;
        public const int PredefinedObjectivesImported = 6014;
        public const int ObjectiveDeleted = 6015;
        public const int CompetitorAdded = 6020;
        public const int CompetitorRemoved = 6021;
        public const int EventGameAdded = 6030;
        public const int CustomEventGameAdded = 6031;
        public const int EventGameRemoved = 6032;
        public const int EventGameEnabled = 6033;
        public const int EventGameDisabled = 6034;
        public const int EventGameUpdated = 6035;
        public const int EventGamesReordered = 6036;
        public const int ObjectivesReordered = 6037;
        public const int StreamerAdded = 6040;
        public const int StreamerRemoved = 6041;
        public const int ModeratorAdded = 6042;
        public const int ModeratorRemoved = 6043;
        public const int ObjectiveCompleted = 6050;
        public const int ObjectiveUncompleted = 6051;
        public const int ObjectiveCompletionTimeEdited = 6052;
        public const int ObjectiveFailed = 6053;
        public const int ObjectiveUnfailed = 6054;
        public const int ObjectivesRemainingFailed = 6055;
        public const int CompetitorInfoAdded = 6060;
        public const int CompetitorInfoUpdated = 6061;
        public const int CompetitorInfoRemoved = 6062;
        public const int RetentionPassCompleted = 7000;
        public const int RetentionPassSkipped = 7001;
        public const int RetentionPassFailed = 7002;
        public const int CorrelationIdRejected = 7003;
        public const int MediaFileMissing = 7004;
        public const int RateLimitRejected = 7005;
    }

    public static class LogEventNames
    {
        public const string TwitchAuthorizationUrlGenerated = nameof(TwitchAuthorizationUrlGenerated);
        public const string TwitchCodeExchangeFailed = nameof(TwitchCodeExchangeFailed);
        public const string TwitchCodeExchanged = nameof(TwitchCodeExchanged);
        public const string TwitchUserInfoFailed = nameof(TwitchUserInfoFailed);
        public const string TwitchUserInfoEmpty = nameof(TwitchUserInfoEmpty);
        public const string TwitchUserInfoRetrieved = nameof(TwitchUserInfoRetrieved);
        public const string TwitchLoginRejected = nameof(TwitchLoginRejected);
        public const string TwitchUserUpserted = nameof(TwitchUserUpserted);
        public const string TwitchExtensionPushSent = nameof(TwitchExtensionPushSent);
        public const string TwitchExtensionPushRejected = nameof(TwitchExtensionPushRejected);
        public const string TwitchExtensionPushFailed = nameof(TwitchExtensionPushFailed);
        public const string TwitchUserUpsertFailed = nameof(TwitchUserUpsertFailed);
        public const string OAuthStateMismatch = nameof(OAuthStateMismatch);
        public const string TwitchUserInfoMalformed = nameof(TwitchUserInfoMalformed);
        public const string JwtAccessTokenGenerated = nameof(JwtAccessTokenGenerated);
        public const string JwtRefreshTokenGenerated = nameof(JwtRefreshTokenGenerated);
        public const string JwtRefreshTokenGenerationFailed = nameof(JwtRefreshTokenGenerationFailed);
        public const string JwtRefreshTokenUnknown = nameof(JwtRefreshTokenUnknown);
        public const string JwtRefreshTokenReuseDetected = nameof(JwtRefreshTokenReuseDetected);
        public const string JwtInactiveRefreshTokenRejected = nameof(JwtInactiveRefreshTokenRejected);
        public const string JwtRefreshTokensRevoked = nameof(JwtRefreshTokensRevoked);
        public const string JwtRefreshTokenRevoked = nameof(JwtRefreshTokenRevoked);
        public const string PredefinedObjectivesSeeded = nameof(PredefinedObjectivesSeeded);
        public const string AdminUserRoleSet = nameof(AdminUserRoleSet);
        public const string AllowlistLoginAdded = nameof(AllowlistLoginAdded);
        public const string AllowlistLoginRemoved = nameof(AllowlistLoginRemoved);
        public const string ApiKeyCreated = nameof(ApiKeyCreated);
        public const string ApiKeyRevoked = nameof(ApiKeyRevoked);
        public const string ConnectorSubmissionProcessed = nameof(ConnectorSubmissionProcessed);
        public const string EventCreated = nameof(EventCreated);
        public const string EventUpdated = nameof(EventUpdated);
        public const string EventArchived = nameof(EventArchived);
        public const string EventUnarchived = nameof(EventUnarchived);
        public const string EventStarted = nameof(EventStarted);
        public const string EventStopped = nameof(EventStopped);
        public const string EventFeatured = nameof(EventFeatured);
        public const string EventUnfeatured = nameof(EventUnfeatured);
        public const string PredefinedObjectiveCreated = nameof(PredefinedObjectiveCreated);
        public const string ObjectiveCreated = nameof(ObjectiveCreated);
        public const string PredefinedObjectiveAssigned = nameof(PredefinedObjectiveAssigned);
        public const string ObjectiveUpdated = nameof(ObjectiveUpdated);
        public const string PredefinedObjectivesImported = nameof(PredefinedObjectivesImported);
        public const string ObjectiveDeleted = nameof(ObjectiveDeleted);
        public const string CompetitorAdded = nameof(CompetitorAdded);
        public const string CompetitorRemoved = nameof(CompetitorRemoved);
        public const string EventGameAdded = nameof(EventGameAdded);
        public const string CustomEventGameAdded = nameof(CustomEventGameAdded);
        public const string EventGameRemoved = nameof(EventGameRemoved);
        public const string EventGameEnabled = nameof(EventGameEnabled);
        public const string EventGameDisabled = nameof(EventGameDisabled);
        public const string EventGameUpdated = nameof(EventGameUpdated);
        public const string EventGamesReordered = nameof(EventGamesReordered);
        public const string ObjectivesReordered = nameof(ObjectivesReordered);
        public const string StreamerAdded = nameof(StreamerAdded);
        public const string StreamerRemoved = nameof(StreamerRemoved);
        public const string ModeratorAdded = nameof(ModeratorAdded);
        public const string ModeratorRemoved = nameof(ModeratorRemoved);
        public const string ObjectiveCompleted = nameof(ObjectiveCompleted);
        public const string ObjectiveUncompleted = nameof(ObjectiveUncompleted);
        public const string ObjectiveCompletionTimeEdited = nameof(ObjectiveCompletionTimeEdited);
        public const string ObjectiveFailed = nameof(ObjectiveFailed);
        public const string ObjectiveUnfailed = nameof(ObjectiveUnfailed);
        public const string ObjectivesRemainingFailed = nameof(ObjectivesRemainingFailed);
        public const string CompetitorInfoAdded = nameof(CompetitorInfoAdded);
        public const string CompetitorInfoUpdated = nameof(CompetitorInfoUpdated);
        public const string CompetitorInfoRemoved = nameof(CompetitorInfoRemoved);
        public const string RetentionPassCompleted = nameof(RetentionPassCompleted);
        public const string RetentionPassSkipped = nameof(RetentionPassSkipped);
        public const string RetentionPassFailed = nameof(RetentionPassFailed);
        public const string CorrelationIdRejected = nameof(CorrelationIdRejected);
        public const string MediaFileMissing = nameof(MediaFileMissing);
        public const string RateLimitRejected = nameof(RateLimitRejected);
    }

    public static class Providers
    {
        public const string Twitch = "twitch";
    }

    public static readonly Counter<long> RequestsProcessed =
        Meter.CreateCounter<long>("soulsjwa.requests.processed", "requests", "Total HTTP requests processed");

    public static readonly Histogram<double> RequestDuration =
        Meter.CreateHistogram<double>("soulsjwa.requests.duration", "ms", "HTTP request processing duration");

    public static readonly Counter<long> BusinessOperations =
        Meter.CreateCounter<long>("soulsjwa.business.operations", "operations", "Total business operations processed");

    public static readonly Histogram<double> BusinessOperationDuration =
        Meter.CreateHistogram<double>("soulsjwa.business.operation.duration", "ms", "Business operation duration");

    /// <summary>
    /// One increment per rule evaluation, tagged with the outcome. A counter
    /// rather than a span: the connector path evaluates every pending rule of
    /// every submission, and a span each was most of the trace volume.
    /// </summary>
    public static readonly Counter<long> RuleEvaluations =
        Meter.CreateCounter<long>("soulsjwa.rules.evaluated", "evaluations", "JsonLogic rule evaluations by outcome");

    public static void RecordRuleEvaluation(string result) =>
        RuleEvaluations.Add(1, new KeyValuePair<string, object?>(Tags.RuleResult, result));

    public static BusinessOperationScope StartBusinessOperation(string operationName, string activityName) =>
        new(operationName, activityName);

    public static void RecordRequest(string method, string route, int statusCode, double elapsedMs)
    {
        var tags = CreateTags(
            (Tags.Method, method),
            (Tags.Route, route),
            (Tags.StatusCode, statusCode));

        RequestsProcessed.Add(1, tags);
        RequestDuration.Record(elapsedMs, tags);
    }

    public static void RecordBusinessOperation(string operationName, string status, double elapsedMs)
    {
        var tags = CreateTags(
            (Tags.Operation, operationName),
            (Tags.Status, status));

        BusinessOperations.Add(1, tags);
        BusinessOperationDuration.Record(elapsedMs, tags);
    }

    private static KeyValuePair<string, object?>[] CreateTags(params (string Key, object? Value)[] tags) =>
        tags.Select(tag => new KeyValuePair<string, object?>(tag.Key, tag.Value)).ToArray();
}

public sealed class BusinessOperationScope : IDisposable
{
    private readonly string _operationName;
    private readonly Stopwatch stopwatch = Stopwatch.StartNew();
    private string status = DiagnosticsConfig.OperationStatuses.Success;
    private bool disposed;

    internal BusinessOperationScope(string operationName, string activityName)
    {
        _operationName = operationName;
        Activity = DiagnosticsConfig.ActivitySource.StartActivity(activityName);
        Activity?.SetTag(DiagnosticsConfig.Tags.Operation, operationName);
    }

    public Activity? Activity { get; }

    public void SetStatus(string status) => this.status = status;

    public void SetTag(string key, object? value) => Activity?.SetTag(key, value);

    public void SetTags(params (string Key, object? Value)[] tags)
    {
        foreach (var tag in tags)
            Activity?.SetTag(tag.Key, tag.Value);
    }

    public void SetError(string status, string? description = null, Exception? exception = null)
    {
        this.status = status;
        Activity?.SetStatus(ActivityStatusCode.Error, description ?? status);
        if (exception is not null)
            Activity?.AddException(exception);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        stopwatch.Stop();
        DiagnosticsConfig.RecordBusinessOperation(_operationName, status, stopwatch.Elapsed.TotalMilliseconds);
        Activity?.Dispose();
    }
}
