using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// The sample-events route over the wire: admin-only, 201 with the created
/// events in catalog order. What gets created is SampleEventSeederTests' subject.
/// </summary>
public class AdminSampleEventsEndpointTests : ApiTestBase
{
    private const string Route = "/api/v1/admin/sample-events";

    [Fact]
    public async Task Admin_Gets201WithTheFiveCreatedEvents_OwnedByThem()
    {
        var (admin, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);

        var response = await client.PostAsync(Route, null);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CreatedDto>();
        body!.Events.Select(e => e.Name).Should().Equal(
            "Sample: Not started",
            "Sample: Game in progress",
            "Sample: Between games",
            "Sample: Many games",
            "Sample: Empty setup");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ids = body.Events.Select(e => e.Id).ToList();
        (await db.Events.CountAsync(e => ids.Contains(e.Id) && e.CreatedById == admin.Id)).Should().Be(5);
    }

    [Fact]
    public async Task PlainUser_Gets403()
    {
        var (_, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "user", UserRole.User);

        var response = await TestAuth.CreateAuthenticatedClient(Factory, key).PostAsync(Route, null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_Gets401()
    {
        var response = await Client.PostAsync(Route, null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private sealed record CreatedDto(List<CreatedEventDto> Events);
    private sealed record CreatedEventDto(Guid Id, string Name);
}
