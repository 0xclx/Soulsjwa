using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// No log line emitted for a request to the overlay-scoreboard endpoint may
/// contain the raw overlay token. Serilog's Console sink writes to
/// <see cref="Console.Out"/>, so redirecting it captures exactly what a real
/// log aggregator would receive — no in-memory sink or production-code hook
/// needed.
/// </summary>
[Collection(ConsoleCaptureCollection.Name)]
public class OverlayTokenLoggingTests : ApiTestBase
{
    [Fact]
    public async Task OverlayScoreboardRequest_ViaQueryToken_DoesNotLogRawTokenToConsole()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        Guid eventId;
        string rawToken;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ev = new Event { Name = "logging-test", CreatedById = owner.Id, IsStarted = true };
            db.Events.Add(ev);
            await db.SaveChangesAsync();
            eventId = ev.Id;

            var (raw, prefix) = Soulsjwa.Api.Features.Events.OverlayToken.Generate();
            rawToken = raw;
            db.EventOverlayTokens.Add(new EventOverlayToken
            {
                EventId = eventId,
                Name = "log-test-token",
                TokenHash = Soulsjwa.Api.Features.Events.OverlayToken.Hash(raw),
                TokenPrefix = prefix,
                CreatedById = owner.Id,
            });
            await db.SaveChangesAsync();
        }

        var originalOut = Console.Out;
        var capture = new ConsoleCapture();
        Console.SetOut(capture);
        try
        {
            var response = await Client.GetAsync($"/api/v1/events/{eventId}/overlay-scoreboard?token={rawToken}");
            response.EnsureSuccessStatusCode();
            // The request-logging middleware flushes asynchronously, so wait
            // for a line to arrive rather than guessing at a duration. That
            // also keeps the negative assertion below honest: it would pass
            // trivially against an empty capture.
            await WaitForCapturedOutputAsync(capture);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var logged = capture.ToString();
        logged.Should().NotBeNullOrWhiteSpace("the request-completion log line should have been captured");
        logged.Should().NotContain(rawToken, "the raw overlay token must never reach application logs");
    }

    [Fact]
    public async Task OverlayScoreboardRequest_ViaHeaderToken_ReturnsSameBodyAsQueryToken()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        Guid eventId;
        string rawToken;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ev = new Event { Name = "header-auth-test", CreatedById = owner.Id, IsStarted = true };
            db.Events.Add(ev);
            await db.SaveChangesAsync();
            eventId = ev.Id;

            var (raw, prefix) = Soulsjwa.Api.Features.Events.OverlayToken.Generate();
            rawToken = raw;
            db.EventOverlayTokens.Add(new EventOverlayToken
            {
                EventId = eventId,
                Name = "header-test-token",
                TokenHash = Soulsjwa.Api.Features.Events.OverlayToken.Hash(raw),
                TokenPrefix = prefix,
                CreatedById = owner.Id,
            });
            await db.SaveChangesAsync();
        }

        var viaQuery = await Client.GetAsync($"/api/v1/events/{eventId}/overlay-scoreboard?token={rawToken}");
        viaQuery.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var queryBody = await viaQuery.Content.ReadAsStringAsync();

        var headerRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/events/{eventId}/overlay-scoreboard");
        headerRequest.Headers.Add(Soulsjwa.Api.Features.Events.OverlayToken.HeaderName, rawToken);
        var viaHeader = await Client.SendAsync(headerRequest);
        viaHeader.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var headerBody = await viaHeader.Content.ReadAsStringAsync();

        headerBody.Should().Be(queryBody);
    }

    [Fact]
    public async Task OverlayScoreboardRequest_ExpiredToken_Returns401IdenticalToUnknownToken()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        Guid eventId;
        string expiredToken;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ev = new Event { Name = "expiry-test", CreatedById = owner.Id, IsStarted = true };
            db.Events.Add(ev);
            await db.SaveChangesAsync();
            eventId = ev.Id;

            var (raw, prefix) = Soulsjwa.Api.Features.Events.OverlayToken.Generate();
            expiredToken = raw;
            db.EventOverlayTokens.Add(new EventOverlayToken
            {
                EventId = eventId,
                Name = "expired-token",
                TokenHash = Soulsjwa.Api.Features.Events.OverlayToken.Hash(raw),
                TokenPrefix = prefix,
                CreatedById = owner.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(-1),
            });
            await db.SaveChangesAsync();
        }

        var expiredResponse = await Client.GetAsync($"/api/v1/events/{eventId}/overlay-scoreboard?token={expiredToken}");
        var unknownResponse = await Client.GetAsync($"/api/v1/events/{eventId}/overlay-scoreboard?token=ot_totallyunknowntoken0000000000");

        expiredResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
        unknownResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
        // Compare the "detail" field rather than the raw body — ASP.NET
        // Core's ProblemDetails writer stamps a per-request traceId that
        // legitimately differs between the two calls.
        var expiredDetail = (await expiredResponse.Content.ReadFromJsonAsync<ProblemDetailsDto>())!.Detail;
        var unknownDetail = (await unknownResponse.Content.ReadFromJsonAsync<ProblemDetailsDto>())!.Detail;
        expiredDetail.Should().Be(unknownDetail);
    }

    private sealed record ProblemDetailsDto(string? Detail);

    [Fact]
    public async Task OverlayScoreboardRequest_SetsNoReferrerPolicy()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        Guid eventId;
        string rawToken;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ev = new Event { Name = "referrer-policy-test", CreatedById = owner.Id, IsStarted = true };
            db.Events.Add(ev);
            await db.SaveChangesAsync();
            eventId = ev.Id;

            var (raw, prefix) = Soulsjwa.Api.Features.Events.OverlayToken.Generate();
            rawToken = raw;
            db.EventOverlayTokens.Add(new EventOverlayToken
            {
                EventId = eventId,
                Name = "referrer-test-token",
                TokenHash = Soulsjwa.Api.Features.Events.OverlayToken.Hash(raw),
                TokenPrefix = prefix,
                CreatedById = owner.Id,
            });
            await db.SaveChangesAsync();
        }

        var response = await Client.GetAsync($"/api/v1/events/{eventId}/overlay-scoreboard?token={rawToken}");

        response.Headers.TryGetValues("Referrer-Policy", out var values).Should().BeTrue();
        values!.Should().ContainSingle().Which.Should().Be("no-referrer");
    }
}
