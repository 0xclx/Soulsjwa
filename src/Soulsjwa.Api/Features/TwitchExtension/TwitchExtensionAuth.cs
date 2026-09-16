using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Soulsjwa.Api.Features.TwitchExtension;

/// <summary>
/// The token contract between Twitch and this server. Twitch signs a JWT per
/// viewer with the extension secret (HS256 over the base64-decoded secret) and
/// the extension front end sends it as a bearer token; the server verifies it
/// and reads the channel, the viewer's role and their opaque id from the
/// claims. The channel id therefore always comes from the token, never from a
/// query parameter, so one channel's viewer cannot read another's settings.
/// The same contract in the other direction — a token this server signs with
/// role <c>external</c> to call Twitch's extension APIs — is
/// <see cref="CreateToken"/>.
/// </summary>
public static class TwitchExtensionAuth
{
    public const string SchemeName = "TwitchExtension";

    /// <summary>Any valid viewer, moderator or broadcaster token for a channel.</summary>
    public const string ViewerPolicy = "TwitchExtensionViewer";

    /// <summary>A token Twitch issued to the channel's own broadcaster.</summary>
    public const string BroadcasterPolicy = "TwitchExtensionBroadcaster";

    /// <summary>Tokens Twitch issues are short-lived; a little skew absorbs clock drift between Twitch and this host.</summary>
    public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    /// <summary>Claim names as Twitch issues them. Inbound claim mapping is off for this scheme so they arrive unrenamed.</summary>
    public static class Claims
    {
        public const string ChannelId = "channel_id";
        public const string Role = "role";
        public const string OpaqueUserId = "opaque_user_id";
        public const string UserId = "user_id";
        public const string PubSubPerms = "pubsub_perms";
    }

    public static class Roles
    {
        public const string Viewer = "viewer";
        public const string Moderator = "moderator";
        public const string Broadcaster = "broadcaster";

        /// <summary>The role of a token the extension's own backend signs to call Twitch.</summary>
        public const string External = "external";
    }

    /// <summary>PubSub targets, as named in the <c>pubsub_perms</c> claim and the send API.</summary>
    public static class PubSubTargets
    {
        /// <summary>Every viewer of one channel.</summary>
        public const string Broadcast = "broadcast";

        /// <summary>Every viewer of every channel the extension is active on.</summary>
        public const string Global = "global";
    }

    /// <summary>The <c>channel_id</c> a global-broadcast token carries.</summary>
    public const string GlobalChannelId = "all";

    public static TokenValidationParameters BuildValidationParameters(TwitchExtensionOptions options) =>
        new()
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = options.SigningKeys.Select(k => new SymmetricSecurityKey(k)).ToList(),
            // Twitch sets neither an issuer nor an audience on extension tokens.
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = ClockSkew,
            NameClaimType = Claims.OpaqueUserId,
            RoleClaimType = Claims.Role,
        };

    public static string? GetChannelId(ClaimsPrincipal principal) =>
        Normalize(principal.FindFirstValue(Claims.ChannelId));

    public static string? GetOpaqueUserId(ClaimsPrincipal principal) =>
        Normalize(principal.FindFirstValue(Claims.OpaqueUserId));

    public static bool IsBroadcaster(ClaimsPrincipal principal) =>
        string.Equals(principal.FindFirstValue(Claims.Role), Roles.Broadcaster, StringComparison.Ordinal);

    /// <summary>
    /// Signs a token the way Twitch does. Two callers: the tests, which need
    /// viewer and broadcaster tokens for a channel, and the push notifier,
    /// which needs the <see cref="Roles.External"/> token Twitch's extension
    /// APIs require (<paramref name="userId"/> must then be the extension
    /// owner's Twitch user id).
    /// </summary>
    public static string CreateToken(
        byte[] signingKey,
        string channelId,
        string role,
        DateTime expiresUtc,
        string? opaqueUserId = null,
        string? userId = null,
        IReadOnlyDictionary<string, string[]>? pubSubPerms = null)
    {
        var claims = new Dictionary<string, object>
        {
            [Claims.ChannelId] = channelId,
            [Claims.Role] = role,
        };
        if (opaqueUserId is not null) claims[Claims.OpaqueUserId] = opaqueUserId;
        if (userId is not null) claims[Claims.UserId] = userId;
        if (pubSubPerms is not null) claims[Claims.PubSubPerms] = pubSubPerms;

        var descriptor = new SecurityTokenDescriptor
        {
            Claims = claims,
            Expires = expiresUtc,
            // Twitch tokens carry exp only; a NotBefore/IssuedAt would only add
            // clock-skew failure modes for no gain.
            IssuedAt = null,
            NotBefore = null,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(signingKey), SecurityAlgorithms.HmacSha256),
        };
        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(descriptor);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
