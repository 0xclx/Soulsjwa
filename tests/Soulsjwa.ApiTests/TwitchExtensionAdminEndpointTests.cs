using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.TwitchExtension.Entities;
using Soulsjwa.Api.Features.TwitchExtension.Services;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// The admin page's HTTP contract: status and rules for admins only, the
/// rules round-trip, and the bundle download is a zip carrying this
/// deployment's API origin. What the rules do to channels is
/// <c>Soulsjwa.IntegrationTests.TwitchExtensionPolicyTests</c>.
/// </summary>
public class TwitchExtensionAdminEndpointTests : ApiTestBase
{
    private const string Route = "/api/v1/admin/twitch-extension";

    [Fact]
    public async Task Status_IsForAdminsOnly()
    {
        var (_, userKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "user", UserRole.User);
        var user = TestAuth.CreateAuthenticatedClient(Factory, userKey);

        (await Client.GetAsync(Route)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await user.GetAsync(Route)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Status_ReportsTheConfiguredExtensionAndTheDefaultRules()
    {
        var (_, adminKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        var admin = TestAuth.CreateAuthenticatedClient(Factory, adminKey);

        var response = await admin.GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AdminDto>();
        body!.Configured.Should().BeTrue();
        body.ClientId.Should().Be(TwitchExtensionTestConfig.ClientId);
        body.CanPush.Should().BeFalse("the test host sets no owner user id, so pushes never touch Twitch");
        body.ExtensionOrigin.Should().Be(TwitchExtensionTestConfig.ExtensionOrigin);
        body.Bundle.Available.Should().BeTrue();
        body.Bundle.ApiUrl.Should().Be("http://localhost:5173");
        body.Settings.AllowChannelEventChoice.Should().BeTrue();
        body.Settings.DefaultScope.Should().Be(nameof(TwitchExtensionScope.AllGames));
    }

    [Fact]
    public async Task Rules_RoundTrip_AndReachTheViewerPayload()
    {
        var (_, adminKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        var admin = TestAuth.CreateAuthenticatedClient(Factory, adminKey);

        var put = await admin.PutAsJsonAsync($"{Route}/settings", new
        {
            allowChannelEventChoice = false,
            allowViewerScopeSwitch = false,
            defaultScope = nameof(TwitchExtensionScope.ActiveGame),
            defaultHighlightChannelCompetitor = true,
            defaultShowTrialProgress = false,
        });

        put.StatusCode.Should().Be(HttpStatusCode.OK);
        put.Headers.ETag.Should().NotBeNull();
        var body = await (await admin.GetAsync(Route)).Content.ReadFromJsonAsync<AdminDto>();
        body!.Settings.AllowViewerScopeSwitch.Should().BeFalse();
        body.Settings.DefaultScope.Should().Be(nameof(TwitchExtensionScope.ActiveGame));

        var viewer = TwitchExtensionTestConfig.ClientWithToken(Factory, TwitchExtensionTestConfig.ViewerToken("42"));
        var board = await (await viewer.GetAsync("/api/v1/twitch-extension/scoreboard")).Content.ReadFromJsonAsync<BoardDto>();
        board!.Policy.AllowViewerScopeSwitch.Should().BeFalse();
        board.Settings.DefaultScope.Should().Be(nameof(TwitchExtensionScope.ActiveGame));
    }

    [Fact]
    public async Task Rules_WithAPinnedGameDefault_Are400()
    {
        var (_, adminKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        var admin = TestAuth.CreateAuthenticatedClient(Factory, adminKey);

        var put = await admin.PutAsJsonAsync($"{Route}/settings", new
        {
            allowChannelEventChoice = true,
            allowViewerScopeSwitch = true,
            defaultScope = nameof(TwitchExtensionScope.PinnedGame),
            defaultHighlightChannelCompetitor = true,
            defaultShowTrialProgress = true,
        });

        put.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Bundle_IsAZipWithTheApiOriginWrittenIn()
    {
        var (_, adminKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        var admin = TestAuth.CreateAuthenticatedClient(Factory, adminKey);

        var response = await admin.GetAsync($"{Route}/bundle");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/zip");
        response.Content.Headers.ContentDisposition!.FileName!.Trim('"').Should().Be(TwitchExtensionBundle.ZipFileName);
        using var zip = new ZipArchive(await response.Content.ReadAsStreamAsync(), ZipArchiveMode.Read);
        zip.Entries.Select(e => e.FullName).Should().Contain("panel.html").And.Contain(TwitchExtensionBundle.ConfigFileName);
        using var reader = new StreamReader(zip.GetEntry(TwitchExtensionBundle.ConfigFileName)!.Open());
        (await reader.ReadToEndAsync()).Should().Contain("\"apiUrl\":\"http://localhost:5173\"");
    }

    private record BundleDto(bool Available, int FileCount, string ApiUrl);
    private record PolicyDto(bool AllowChannelEventChoice, bool AllowViewerScopeSwitch, string DefaultScope, bool DefaultHighlightChannelCompetitor, bool DefaultShowTrialProgress);
    private record AdminDto(bool Configured, string? ClientId, bool CanPush, string? ExtensionOrigin, BundleDto Bundle, PolicyDto Settings);
    private record SettingsDto(string DefaultScope);
    private record BoardDto(PolicyDto Policy, SettingsDto Settings);
}
