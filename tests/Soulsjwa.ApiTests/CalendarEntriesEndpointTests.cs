using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// The calendar-entry routes over the wire: the owner gate, the anonymous read,
/// and that the JSON binder accepts every timestamp form. Validation, colours
/// and stored instants live in <c>Soulsjwa.IntegrationTests.ScheduleTests</c>.
/// </summary>
public class CalendarEntriesEndpointTests : ApiTestBase
{
    [Fact]
    public async Task Create_AsOwner_Returns201()
    {
        var (owner, ownerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var ev = await SeedEventAsync(owner.Id);
        var client = TestAuth.CreateAuthenticatedClient(Factory, ownerKey);

        var response = await client.PostAsJsonAsync($"/api/v1/events/{ev.Id}/calendar-entries", new
        {
            title = "Grand Finals",
            startsAt = "2026-10-01T18:00:00Z",
            endsAt = "2026-10-01T20:00:00Z",
            isAllDay = false,
            isHighlighted = true,
            color = "Accent",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<EntryDto>();
        body!.Title.Should().Be("Grand Finals");
        body.Color.Should().Be("Accent");
    }

    [Theory]
    [InlineData("2026-10-01T18:00:00Z")]
    [InlineData("2026-10-01T20:00:00+02:00")]
    [InlineData("2026-10-01T18:00:00")]
    public async Task Create_AcceptsZuluOffsetAndZonelessTimestamps(string startsAt)
    {
        // DateTimeOffset request DTOs must accept all three forms instead of
        // 500ing on the zone-less one (Npgsql throws writing an
        // Unspecified-Kind DateTime to a timestamptz column).
        var (owner, ownerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var ev = await SeedEventAsync(owner.Id);
        var client = TestAuth.CreateAuthenticatedClient(Factory, ownerKey);

        var response = await client.PostAsJsonAsync($"/api/v1/events/{ev.Id}/calendar-entries", new
        {
            title = "Format test",
            startsAt,
            endsAt = "2026-10-01T22:00:00Z",
            isAllDay = false,
            isHighlighted = false,
            color = "Default",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task List_IsAnonymouslyReadable()
    {
        var (owner, ownerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var ev = await SeedEventAsync(owner.Id);
        var client = TestAuth.CreateAuthenticatedClient(Factory, ownerKey);
        await client.PostAsJsonAsync($"/api/v1/events/{ev.Id}/calendar-entries", new
        {
            title = "Visible",
            startsAt = "2026-10-01T18:00:00Z",
            endsAt = "2026-10-01T20:00:00Z",
            isAllDay = false,
            isHighlighted = false,
            color = "Default",
        });

        var anon = Factory.CreateClient();
        var list = await anon.GetFromJsonAsync<List<EntryDto>>($"/api/v1/events/{ev.Id}/calendar-entries");

        list.Should().ContainSingle(e => e.Title == "Visible");
    }

    private async Task<Event> SeedEventAsync(Guid ownerId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ev = new Event { Name = "test", CreatedById = ownerId };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return ev;
    }

    private sealed record EntryDto(
        Guid Id, Guid EventId, string Title, string? DescriptionMarkdown,
        DateTime StartsAt, DateTime EndsAt, bool IsAllDay, bool IsHighlighted,
        string Color, Guid? ImageAssetId, string? ImageUrl,
        Guid CreatedById, DateTime CreatedAt, DateTime UpdatedAt);
}
