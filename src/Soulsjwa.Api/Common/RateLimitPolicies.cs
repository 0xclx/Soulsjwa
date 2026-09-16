namespace Soulsjwa.Api.Common;

/// <summary>
/// Centralised so a policy name and its budget can't drift apart between Program.cs
/// and the endpoint files that reference the name by string.
/// </summary>
public static class RateLimitPolicies
{
    public const string Auth = "auth";
    public const string Connector = "connector";
    public const string TwitchExtension = "twitch-extension";

    /// <summary>Per authenticated user, global fallback limiter. Override: <c>RateLimits:GlobalPerUserPerMinute</c>.</summary>
    public const int DefaultGlobalPerUserPermitLimit = 100;

    /// <summary>Per IP for anonymous traffic, global fallback limiter. Override: <c>RateLimits:GlobalPerIpPerMinute</c>.</summary>
    public const int DefaultGlobalPerIpPermitLimit = 60;

    /// <summary>Per IP for the <see cref="Auth"/> policy. Override: <c>RateLimits:AuthPerIpPerMinute</c>.</summary>
    public const int DefaultAuthPermitLimit = 20;

    /// <summary>
    /// Per user for the <see cref="Connector"/> policy. Override:
    /// <c>RateLimits:ConnectorPerMinute</c>. The connector polls memory every 2s and
    /// submits only on state changes, so sustained traffic tops out near 30/min —
    /// this leaves 4x headroom for bursts.
    /// </summary>
    public const int DefaultConnectorPermitLimit = 120;

    /// <summary>
    /// Per viewer for the <see cref="TwitchExtension"/> policy, keyed on the
    /// opaque viewer id in the Twitch-issued token. Override:
    /// <c>RateLimits:TwitchExtensionPerViewerPerMinute</c>. The panel polls
    /// every 5s while an event runs (12/min) plus an occasional detail fetch;
    /// this leaves 3x headroom. Per viewer rather than per IP so a shared NAT
    /// full of viewers is not one bucket.
    /// </summary>
    public const int DefaultTwitchExtensionPermitLimit = 40;

    /// <summary>Partition key fallback when no remote IP is available on the connection.</summary>
    public const string UnknownIpPartition = "unknown";

    /// <summary>Partition key fallback when no authenticated user id is available.</summary>
    public const string AnonymousUserPartition = "anon";

    /// <summary>
    /// Partition for requests exempted from the global fallback limiter because
    /// their endpoint carries a named policy — a no-op limiter, so the key itself
    /// is never rationed.
    /// </summary>
    public const string ExemptFromGlobalLimiterPartition = "exempt";
}
