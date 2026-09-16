using Soulsjwa.Api.Features.TwitchExtension;

namespace Soulsjwa.ApiTests;

/// <summary>
/// The extension the test host is configured to back (see
/// <see cref="ApiTestBase"/>'s environment defaults) and a way to mint the
/// tokens Twitch would issue for it.
/// </summary>
public static class TwitchExtensionTestConfig
{
    public const string ClientId = "testextensionclientid";

    private static readonly byte[] Secret = Enumerable.Range(0, 32).Select(i => (byte)(i * 7)).ToArray();

    public static readonly string SecretBase64 = Convert.ToBase64String(Secret);

    /// <summary>The origin Twitch serves this extension's front end from.</summary>
    public static readonly string ExtensionOrigin = $"https://{ClientId}.ext-twitch.tv";

    /// <summary>
    /// A stand-in for the built front end (the Docker image's frontend stage
    /// produces the real one), so the admin bundle download has something to
    /// zip. Created once per test process; never deleted, it is a temp dir.
    /// </summary>
    public static readonly string BundlePath = CreateBundleDirectory();

    private static string CreateBundleDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"soulsjwa-twitch-bundle-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "assets"));
        File.WriteAllText(Path.Combine(root, "panel.html"), "<html>test panel</html>");
        File.WriteAllText(Path.Combine(root, "assets", "mount.js"), "// test bundle");
        return root;
    }

    public static string ViewerToken(string channelId, string opaqueUserId = "U1") =>
        TwitchExtensionAuth.CreateToken(
            Secret, channelId, TwitchExtensionAuth.Roles.Viewer, DateTime.UtcNow.AddMinutes(5), opaqueUserId);

    public static string BroadcasterToken(string channelId) =>
        TwitchExtensionAuth.CreateToken(
            Secret, channelId, TwitchExtensionAuth.Roles.Broadcaster, DateTime.UtcNow.AddMinutes(5), $"U{channelId}", channelId);

    /// <summary>A token signed with some other extension's secret — Twitch would never issue it for this one.</summary>
    public static string ForeignToken(string channelId) =>
        TwitchExtensionAuth.CreateToken(
            Enumerable.Repeat((byte)0xAB, 32).ToArray(), channelId, TwitchExtensionAuth.Roles.Viewer, DateTime.UtcNow.AddMinutes(5));

    public static HttpClient ClientWithToken(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("Origin", ExtensionOrigin);
        return client;
    }
}
