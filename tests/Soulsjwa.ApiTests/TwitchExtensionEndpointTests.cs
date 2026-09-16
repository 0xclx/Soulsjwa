using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Features.TwitchExtension.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// The extension backend's HTTP contract: Twitch-issued bearer tokens are the
/// credential, the viewer read is cached and revalidated by ETag, the
/// configuration write is broadcaster-only and needs a linked account, and
/// the Twitch CDN origin passes CORS. Which event a channel resolves to and
/// what a save may contain are <c>Soulsjwa.IntegrationTests.TwitchExtensionTests</c>.
/// </summary>
public class TwitchExtensionEndpointTests : ApiTestBase
{
    private const string ChannelId = "1122334455";

    [Fact]
    public async Task Status_IsAnonymous_AndNamesTheExtension()
    {
        var response = await Client.GetAsync("/api/v1/twitch-extension/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<StatusDto>();
        body.Should().BeEquivalentTo(new StatusDto(true, TwitchExtensionTestConfig.ClientId));
    }

    [Fact]
    public async Task Scoreboard_WithoutAToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/twitch-extension/scoreboard");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Scoreboard_WithATokenFromAnotherExtension_Returns401()
    {
        var client = TwitchExtensionTestConfig.ClientWithToken(Factory, TwitchExtensionTestConfig.ForeignToken(ChannelId));

        var response = await client.GetAsync("/api/v1/twitch-extension/scoreboard");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ASessionApiKey_DoesNotOpenTheExtensionRoutes()
    {
        var (_, apiKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        var client = TestAuth.CreateAuthenticatedClient(Factory, apiKey);

        var response = await client.GetAsync("/api/v1/twitch-extension/scoreboard");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "only the Twitch scheme is accepted there");
    }

    [Fact]
    public async Task Scoreboard_WithAViewerToken_ReturnsTheFeaturedEvent_AndRevalidatesByETag()
    {
        var ev = await SeedFeaturedEventAsync();
        var client = TwitchExtensionTestConfig.ClientWithToken(Factory, TwitchExtensionTestConfig.ViewerToken(ChannelId));

        var response = await client.GetAsync("/api/v1/twitch-extension/scoreboard");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag.Should().NotBeNull();
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Contain(TwitchExtensionTestConfig.ExtensionOrigin);
        var body = await response.Content.ReadFromJsonAsync<ScoreboardDto>();
        body!.ChannelId.Should().Be(ChannelId);
        body.Event!.Id.Should().Be(ev.Id);
        body.Event.Source.Should().Be("Featured");

        using var revalidate = new HttpRequestMessage(HttpMethod.Get, "/api/v1/twitch-extension/scoreboard");
        revalidate.Headers.IfNoneMatch.Add(response.Headers.ETag!);
        var notModified = await client.SendAsync(revalidate);

        notModified.StatusCode.Should().Be(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task Scoreboard_WithNothingToShow_IsAnEmpty200()
    {
        var client = TwitchExtensionTestConfig.ClientWithToken(Factory, TwitchExtensionTestConfig.ViewerToken("5"));

        var response = await client.GetAsync("/api/v1/twitch-extension/scoreboard");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ScoreboardDto>();
        body!.Event.Should().BeNull();
    }

    [Fact]
    public async Task CompetitorDetail_ForAStranger_Returns404()
    {
        await SeedFeaturedEventAsync();
        var client = TwitchExtensionTestConfig.ClientWithToken(Factory, TwitchExtensionTestConfig.ViewerToken(ChannelId));

        var response = await client.GetAsync($"/api/v1/twitch-extension/scoreboard/competitors/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Configuration_WithAViewerToken_Returns403()
    {
        var client = TwitchExtensionTestConfig.ClientWithToken(Factory, TwitchExtensionTestConfig.ViewerToken(ChannelId));

        var read = await client.GetAsync("/api/v1/twitch-extension/configuration");
        var write = await client.PutAsJsonAsync("/api/v1/twitch-extension/configuration",
            new { eventId = (Guid?)null, defaultScope = nameof(TwitchExtensionScope.AllGames), pinnedEventGameId = (Guid?)null });

        read.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        write.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Configuration_ByAnUnlinkedBroadcaster_ReadsButCannotSave()
    {
        var client = TwitchExtensionTestConfig.ClientWithToken(Factory, TwitchExtensionTestConfig.BroadcasterToken(ChannelId));

        var read = await client.GetAsync("/api/v1/twitch-extension/configuration");
        var write = await client.PutAsJsonAsync("/api/v1/twitch-extension/configuration",
            new { eventId = (Guid?)null, defaultScope = nameof(TwitchExtensionScope.ActiveGame), pinnedEventGameId = (Guid?)null });

        read.StatusCode.Should().Be(HttpStatusCode.OK);
        (await read.Content.ReadFromJsonAsync<ConfigurationDto>())!.LinkedUser.Should().BeNull();
        write.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await write.Content.ReadAsStringAsync()).Should().Contain("Sign in to Soulsjwa");
    }

    [Fact]
    public async Task Configuration_ByALinkedBroadcaster_Saves()
    {
        var ev = await SeedFeaturedEventAsync();
        await LinkChannelAsync(ChannelId);
        var client = TwitchExtensionTestConfig.ClientWithToken(Factory, TwitchExtensionTestConfig.BroadcasterToken(ChannelId));

        var write = await client.PutAsJsonAsync("/api/v1/twitch-extension/configuration",
            new
            {
                eventId = ev.Id,
                defaultScope = nameof(TwitchExtensionScope.ActiveGame),
                pinnedEventGameId = (Guid?)null,
                highlightChannelCompetitor = false,
            });

        write.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await write.Content.ReadFromJsonAsync<ConfigurationDto>();
        body!.LinkedUser.Should().NotBeNull();
        body.Settings.EventId.Should().Be(ev.Id);
        body.Settings.DefaultScope.Should().Be(nameof(TwitchExtensionScope.ActiveGame));
        body.Settings.HighlightChannelCompetitor.Should().BeFalse();

        var viewer = TwitchExtensionTestConfig.ClientWithToken(Factory, TwitchExtensionTestConfig.ViewerToken(ChannelId));
        var scoreboard = await (await viewer.GetAsync("/api/v1/twitch-extension/scoreboard")).Content.ReadFromJsonAsync<ScoreboardDto>();
        scoreboard!.Event!.Source.Should().Be("Explicit");
    }

    [Fact]
    public async Task Configuration_WithABadScope_Is400()
    {
        await LinkChannelAsync(ChannelId);
        var client = TwitchExtensionTestConfig.ClientWithToken(Factory, TwitchExtensionTestConfig.BroadcasterToken(ChannelId));

        var write = await client.PutAsJsonAsync("/api/v1/twitch-extension/configuration",
            new { eventId = (Guid?)null, defaultScope = "Everything", pinnedEventGameId = (Guid?)null });

        write.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MyConfiguration_UsesTheSignedInUsersChannel()
    {
        var (user, apiKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "streamer");
        var client = TestAuth.CreateAuthenticatedClient(Factory, apiKey);

        var read = await client.GetAsync("/api/v1/me/twitch-extension");
        var write = await client.PutAsJsonAsync("/api/v1/me/twitch-extension",
            new { eventId = (Guid?)null, defaultScope = nameof(TwitchExtensionScope.ActiveGame), pinnedEventGameId = (Guid?)null });

        read.StatusCode.Should().Be(HttpStatusCode.OK);
        (await read.Content.ReadFromJsonAsync<ConfigurationDto>())!.ChannelId.Should().Be(user.TwitchId);
        write.StatusCode.Should().Be(HttpStatusCode.OK);
        (await write.Content.ReadFromJsonAsync<ConfigurationDto>())!.Settings.DefaultScope.Should().Be(nameof(TwitchExtensionScope.ActiveGame));
    }

    [Fact]
    public async Task MyConfiguration_IsNotAnonymous()
    {
        var response = await Client.GetAsync("/api/v1/me/twitch-extension");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Preflight_FromTheExtensionOrigin_IsAllowed()
    {
        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/v1/twitch-extension/scoreboard");
        preflight.Headers.Add("Origin", TwitchExtensionTestConfig.ExtensionOrigin);
        preflight.Headers.Add("Access-Control-Request-Method", "GET");
        preflight.Headers.Add("Access-Control-Request-Headers", "authorization,if-none-match");

        var response = await Client.SendAsync(preflight);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Contain(TwitchExtensionTestConfig.ExtensionOrigin);
        response.Headers.GetValues("Access-Control-Allow-Headers").Single().ToLowerInvariant()
            .Should().Contain("authorization").And.Contain("if-none-match");
    }

    [Fact]
    public async Task Preflight_FromAnotherOrigin_IsNotAllowed()
    {
        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/v1/twitch-extension/scoreboard");
        preflight.Headers.Add("Origin", "https://evil.example");
        preflight.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await Client.SendAsync(preflight);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    private async Task<Event> SeedFeaturedEventAsync()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ev = new Event { Name = "featured", CreatedById = owner.Id, IsFeatured = true, IsStarted = true };
        ev.EventGames.Add(new EventGame { CustomGameName = "Custom", IsEnabled = true });
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return ev;
    }

    /// <summary>A Soulsjwa account signed in with the channel's Twitch account, which saving requires.</summary>
    private async Task LinkChannelAsync(string channelId)
    {
        var (user, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "linked");
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Users.Attach(user).Entity.TwitchId = channelId;
        await db.SaveChangesAsync();
    }

    private record StatusDto(bool Configured, string? ClientId);
    private record EventDto(Guid Id, string Name, string Source);
    private record ScoreboardDto(string ChannelId, EventDto? Event);
    private record SettingsDto(Guid? EventId, string DefaultScope, Guid? PinnedEventGameId, bool HighlightChannelCompetitor, bool ShowTrialProgress);
    private record LinkedUserDto(Guid Id, string DisplayName);
    private record ConfigurationDto(string ChannelId, LinkedUserDto? LinkedUser, SettingsDto Settings);
}
