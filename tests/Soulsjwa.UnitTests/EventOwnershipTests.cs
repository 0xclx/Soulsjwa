using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Soulsjwa.Api.Features.Events;
using Soulsjwa.Api.Features.Events.Entities;
using Xunit;

namespace Soulsjwa.UnitTests;

public class EventOwnershipTests
{
    [Fact]
    public void GetUserId_ParsesNameIdentifierClaim()
    {
        var id = Guid.NewGuid();
        EventOwnership.GetUserId(Principal(id)).Should().Be(id);
    }

    [Fact]
    public void GetUserId_MissingClaim_Throws()
    {
        var p = new ClaimsPrincipal(new ClaimsIdentity());
        var act = () => EventOwnership.GetUserId(p);
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void GetUserId_NonGuidClaim_Throws()
    {
        var p = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "not-a-guid") }, "test"));
        var act = () => EventOwnership.GetUserId(p);
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void TryGetUserId_ValidClaim_ReturnsTrueAndParsedId()
    {
        var id = Guid.NewGuid();
        EventOwnership.TryGetUserId(Principal(id), out var userId).Should().BeTrue();
        userId.Should().Be(id);
    }

    [Fact]
    public void TryGetUserId_MissingClaim_ReturnsFalse()
    {
        var p = new ClaimsPrincipal(new ClaimsIdentity());
        EventOwnership.TryGetUserId(p, out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetUserId_NonGuidClaim_ReturnsFalse()
    {
        var p = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "not-a-guid") }, "test"));
        EventOwnership.TryGetUserId(p, out _).Should().BeFalse();
    }

    [Fact]
    public void RequireOwner_OwnerMatch_ReturnsNull()
    {
        var owner = Guid.NewGuid();
        var ev = new Event { CreatedById = owner };
        EventOwnership.RequireOwner(ev, Principal(owner), "do anything").Should().BeNull();
    }

    [Fact]
    public async Task RequireOwner_NotOwner_Returns403WithActionInDetail()
    {
        var ev = new Event { CreatedById = Guid.NewGuid() };
        var result = EventOwnership.RequireOwner(ev, Principal(Guid.NewGuid()), "delete this event");
        result.Should().NotBeNull();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();
        var ctx = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            Response = { Body = new MemoryStream() },
        };
        await result!.ExecuteAsync(ctx);

        ctx.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        ctx.Response.Body.Position = 0;
        var body = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
        body.Should().Contain("delete this event");
    }

    [Fact]
    public void RequireOwner_AdminPrincipal_AlwaysPasses()
    {
        var ev = new Event { CreatedById = Guid.NewGuid() };
        EventOwnership.RequireOwner(ev, AdminPrincipal(Guid.NewGuid()), "do anything").Should().BeNull();
    }

    [Fact]
    public void RequireOwner_AssignedStreamer_DoesNotAutomaticallyPass()
    {
        // Streamers are participants, not event managers — they need explicit
        // admin permission for management actions on the event itself.
        var streamer = Guid.NewGuid();
        var ev = new Event { CreatedById = Guid.NewGuid() };
        EventOwnership.RequireOwner(ev, Principal(streamer), "do anything").Should().NotBeNull();
    }

    [Fact]
    public void IsAdmin_AcceptsBothRoleClaimAndClaimTypesRole()
    {
        var raw = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("role", "Admin") }, "test"));
        EventOwnership.IsAdmin(raw).Should().BeTrue();

        var mapped = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Role, "Admin") }, "test"));
        EventOwnership.IsAdmin(mapped).Should().BeTrue();

        var none = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("role", "User") }, "test"));
        EventOwnership.IsAdmin(none).Should().BeFalse();
    }

    private static ClaimsPrincipal Principal(Guid userId) =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "test"));

    private static ClaimsPrincipal AdminPrincipal(Guid userId) =>
        new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("role", "Admin"),
        }, "test"));
}
