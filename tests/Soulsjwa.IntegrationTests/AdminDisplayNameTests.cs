using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Soulsjwa.Api.Features.Admin.Endpoints;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Users.Endpoints;
using Xunit;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// Admins rename anyone through the same rules as the self-service route;
/// the audit names the admin as actor and the renamed user as subject.
/// </summary>
public class AdminDisplayNameTests : IntegrationTestBase
{
    private Task<Microsoft.AspNetCore.Http.IResult> RenameAsync(User caller, Guid targetId, string? displayName) =>
        AdminUsersEndpoint.SetDisplayName(
            targetId, new UpdateDisplayNameRequest(displayName), caller.Principal(), CreateDbContext(),
            Audit, Cache, NullLogger<AdminUsersEndpoint>.Instance, default);

    [Fact]
    public async Task Admin_RenamesAnotherUser_AndIsTheAuditActor()
    {
        var db = CreateDbContext();
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);
        var target = await Fixtures.AddUserAsync(db, "target");

        var result = await RenameAsync(admin, target.Id, "Siegmeyer");

        result.Status().Should().Be(200);
        var body = result.Value<AdminUserResponse>();
        body.DisplayName.Should().Be("Siegmeyer");
        body.DisplayNameOverride.Should().Be("Siegmeyer");
        body.TwitchDisplayName.Should().Be(target.TwitchDisplayName);
        var audit = await CreateDbContext().AuditLogs.SingleAsync(a => a.Type == AuditEventTypes.UserDisplayNameChanged);
        audit.ActorUserId.Should().Be(admin.Id);
        audit.SubjectUserId.Should().Be(target.Id);
    }

    [Fact]
    public async Task NonAdmin_Is403_EvenForThemselves()
    {
        var db = CreateDbContext();
        var user = await Fixtures.AddUserAsync(db, "plain");

        var result = await RenameAsync(user, user.Id, "Self");

        result.Status().Should().Be(403);
        (await CreateDbContext().Users.SingleAsync(u => u.Id == user.Id)).DisplayNameOverride.Should().BeNull();
    }

    [Fact]
    public async Task UnknownUser_Is404()
    {
        var admin = await Fixtures.AddUserAsync(CreateDbContext(), "admin", UserRole.Admin);

        (await RenameAsync(admin, Guid.NewGuid(), "Nobody")).Status().Should().Be(404);
    }

    [Fact]
    public async Task InvalidName_Is400()
    {
        var db = CreateDbContext();
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);
        var target = await Fixtures.AddUserAsync(db, "target");

        var result = await RenameAsync(admin, target.Id, "bad\nname");

        result.Status().Should().Be(400);
        result.ValidationErrors().Should().ContainKey("displayName");
    }
}
