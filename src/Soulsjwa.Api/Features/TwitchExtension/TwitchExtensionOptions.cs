using System.Text.RegularExpressions;

namespace Soulsjwa.Api.Features.TwitchExtension;

/// <summary>
/// Settings for the Twitch Extension this server backs (its "extension backend
/// service"). Everything is optional: with no client id and no secret the
/// feature is off — the extension route group is not mapped (so its routes
/// 404 like any unknown <c>/api/v1</c> path), the web app hides its card and
/// nothing else changes. Read once at startup and shared by
/// <c>Program.cs</c> (which needs the signing keys to register the bearer
/// scheme) and the endpoints, so the two can never disagree about what is
/// configured.
/// </summary>
public sealed partial class TwitchExtensionOptions
{
    public const string SectionName = "TwitchExtension";
    public const string ClientIdKey = SectionName + ":ClientId";

    /// <summary>The single-secret form; the common case.</summary>
    public const string SecretKey = SectionName + ":Secret";

    /// <summary>
    /// The multi-secret form, for a rotation: Twitch keeps the previous secret
    /// valid for about an hour after a new one is generated, so both are
    /// listed until every viewer's token was issued with the new one.
    /// </summary>
    public const string SecretsKey = SectionName + ":Secrets";

    /// <summary>
    /// Twitch user id of the extension's owner. Only the push notifier needs
    /// it: Twitch requires the <c>user_id</c> of a server-signed token to be
    /// the owner's, so without it the server never signs one and viewers poll.
    /// </summary>
    public const string OwnerUserIdKey = SectionName + ":OwnerUserId";

    /// <summary>
    /// An extra origin to allow on the extension CORS policy, for Twitch's
    /// Local Test mode where the front end is served from the developer's own
    /// machine (default <c>https://localhost:8080</c>) rather than Twitch's CDN.
    /// Never set it in production.
    /// </summary>
    public const string LocalTestOriginKey = SectionName + ":LocalTestOrigin";

    /// <summary>Twitch serves every extension's assets from this host pattern, so it is the iframe's origin.</summary>
    private const string ExtensionOriginFormat = "https://{0}.ext-twitch.tv";

    private TwitchExtensionOptions(
        string? clientId, IReadOnlyList<byte[]> signingKeys, string? ownerUserId, string? localTestOrigin)
    {
        ClientId = clientId;
        SigningKeys = signingKeys;
        OwnerUserId = ownerUserId;
        LocalTestOrigin = localTestOrigin;
    }

    public string? ClientId { get; }

    /// <summary>The base64-decoded extension secrets, newest first — the raw bytes Twitch signs viewer tokens with.</summary>
    public IReadOnlyList<byte[]> SigningKeys { get; }

    public string? OwnerUserId { get; }

    public string? LocalTestOrigin { get; }

    public bool IsConfigured => ClientId is not null && SigningKeys.Count > 0;

    /// <summary>True when the server can sign its own tokens to call Twitch's extension APIs.</summary>
    public bool CanPush => IsConfigured && OwnerUserId is not null;

    /// <summary>Origins the extension front end can call this API from: Twitch's CDN, plus the local-test one when set.</summary>
    public IReadOnlyList<string> AllowedOrigins
    {
        get
        {
            if (!IsConfigured) return [];
            var origins = new List<string> { string.Format(ExtensionOriginFormat, ClientId) };
            if (LocalTestOrigin is not null) origins.Add(LocalTestOrigin);
            return origins;
        }
    }

    /// <summary>An unconfigured instance, for tests and for the <c>--migrate</c> process.</summary>
    public static TwitchExtensionOptions Disabled { get; } = new(null, [], null, null);

    /// <summary>
    /// Reads and validates the section. Half a configuration (a client id
    /// without a secret, or the reverse) throws rather than silently running
    /// with the feature off, since that is always a deployment mistake.
    /// </summary>
    public static TwitchExtensionOptions FromConfiguration(IConfiguration configuration)
    {
        var clientId = Normalize(configuration[ClientIdKey]);
        var secrets = new List<string>();
        if (Normalize(configuration[SecretKey]) is { } single) secrets.Add(single);
        secrets.AddRange(configuration.GetSection(SecretsKey).GetChildren()
            .Select(c => Normalize(c.Value))
            .OfType<string>());

        if (clientId is null && secrets.Count == 0)
            return Disabled;

        if (clientId is null)
            throw new InvalidOperationException($"{ClientIdKey} must be configured when a Twitch extension secret is set.");
        if (secrets.Count == 0)
            throw new InvalidOperationException($"{SecretKey} (or {SecretsKey}) must be configured when {ClientIdKey} is set.");
        if (!ClientIdPattern().IsMatch(clientId))
            throw new InvalidOperationException($"{ClientIdKey} must be alphanumeric; got '{clientId}'.");

        var keys = new List<byte[]>(secrets.Count);
        foreach (var secret in secrets)
        {
            try
            {
                keys.Add(Convert.FromBase64String(secret));
            }
            catch (FormatException)
            {
                throw new InvalidOperationException(
                    $"{SecretKey} must be the base64 value shown in the Twitch developer console, not a decoded or hex string.");
            }
        }

        var ownerUserId = Normalize(configuration[OwnerUserIdKey]);
        if (ownerUserId is not null && !TwitchUserIdPattern().IsMatch(ownerUserId))
            throw new InvalidOperationException($"{OwnerUserIdKey} must be a numeric Twitch user id; got '{ownerUserId}'.");

        var localTestOrigin = Normalize(configuration[LocalTestOriginKey]);
        if (localTestOrigin is not null
            && (!Uri.TryCreate(localTestOrigin, UriKind.Absolute, out var uri) || uri.PathAndQuery != "/"))
            throw new InvalidOperationException($"{LocalTestOriginKey} must be an origin such as https://localhost:8080; got '{localTestOrigin}'.");

        return new TwitchExtensionOptions(clientId, keys, ownerUserId, localTestOrigin?.TrimEnd('/'));
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex("^[A-Za-z0-9]+$")]
    private static partial Regex ClientIdPattern();

    [GeneratedRegex("^[0-9]+$")]
    private static partial Regex TwitchUserIdPattern();
}
