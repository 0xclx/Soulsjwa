using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Soulsjwa.Api.Features.Admin.Endpoints;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events.Endpoints;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// Competitor membership, streamer delegation, and the admin-managed allowlist
/// and role list underneath them. Needs real Postgres: the placeholder user for
/// an unknown Twitch handle is arbitrated by unique indexes, removing a
/// competitor cascades their moderator delegations away, and revoking an
/// allowlist entry must reach the user's refresh tokens in the same transaction.
/// The wire contract is <c>Soulsjwa.ApiTests.EventCompetitorsEndpointTests</c>
/// and <c>PermissionsEndpointTests</c>.
/// </summary>
public class CompetitorAndDelegationTests : IntegrationTestBase
{
    [Fact]
    public async Task AddCompetitor_ByANonOwner_IsForbidden()
    {
        var db = CreateDbContext();
        var (admin, ev) = await AddEventAsync(db);
        var stranger = await Fixtures.AddUserAsync(db, "stranger");
        var target = await Fixtures.AddUserAsync(db, "target");

        var result = await AddCompetitorAsync(db, ev, stranger, new AddCompetitorRequest(target.Id, null));

        result.Status().Should().Be(StatusCodes.Status403Forbidden);
        admin.Should().NotBeNull();
    }

    [Fact]
    public async Task AddCompetitor_Twice_Conflicts()
    {
        var db = CreateDbContext();
        var (admin, ev) = await AddEventAsync(db);
        var target = await Fixtures.AddUserAsync(db, "target");

        (await AddCompetitorAsync(db, ev, admin, new AddCompetitorRequest(target.Id, null)))
            .Status().Should().Be(StatusCodes.Status201Created);
        var duplicate = await AddCompetitorAsync(CreateDbContext(), ev, admin, new AddCompetitorRequest(target.Id, null));

        duplicate.Status().Should().Be(StatusCodes.Status409Conflict);
        (await CreateDbContext().EventCompetitors.CountAsync(c => c.EventId == ev.Id)).Should().Be(1);
    }

    [Fact]
    public async Task AddCompetitor_ToAnEventThatDoesNotExist_IsNotFound()
    {
        var db = CreateDbContext();
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);
        var target = await Fixtures.AddUserAsync(db, "target");

        var result = await EventCompetitorsEndpoint.AddCompetitor(
            Guid.NewGuid(), new AddCompetitorRequest(target.Id, null), admin.Principal(),
            db, Audit, NullLogger<EventCompetitorsEndpoint>.Instance, Cache, default);

        result.Status().Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task AddCompetitor_ForAUserIdThatDoesNotExist_IsNotFound()
    {
        var db = CreateDbContext();
        var (admin, ev) = await AddEventAsync(db);

        var result = await AddCompetitorAsync(db, ev, admin, new AddCompetitorRequest(Guid.NewGuid(), null));

        result.Status().Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task AddCompetitor_WithNeitherAnIdNorAHandle_IsRejected()
    {
        var db = CreateDbContext();
        var (admin, ev) = await AddEventAsync(db);

        var result = await AddCompetitorAsync(db, ev, admin, new AddCompetitorRequest(null, null));

        result.Status().Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task AddCompetitor_ByTwitchLogin_FindsAnExistingUserCaseInsensitively()
    {
        var db = CreateDbContext();
        var (admin, ev) = await AddEventAsync(db);
        var target = await Fixtures.AddUserAsync(db, "target");

        var result = await AddCompetitorAsync(
            db, ev, admin, new AddCompetitorRequest(null, target.TwitchLogin.ToUpperInvariant()));

        result.Status().Should().Be(StatusCodes.Status201Created);
        (await CreateDbContext().EventCompetitors.AnyAsync(c => c.EventId == ev.Id && c.UserId == target.Id))
            .Should().BeTrue("the lookup compares LOWER(\"TwitchLogin\"), which is what the functional index serves");
    }

    [Fact]
    public async Task AddCompetitor_ByAnUnknownHandle_CreatesAPlaceholderUserAndAllowlistsIt()
    {
        var db = CreateDbContext();
        var (admin, ev) = await AddEventAsync(db);
        var handle = $"newcomer{Guid.NewGuid():N}"[..20];

        var result = await AddCompetitorAsync(db, ev, admin, new AddCompetitorRequest(null, handle));

        result.Status().Should().Be(StatusCodes.Status201Created);
        var after = CreateDbContext();
        var placeholder = await after.Users.SingleAsync(u => u.TwitchLogin == handle.ToLowerInvariant());
        placeholder.IsAllowlisted.Should().BeTrue("the invitation is what grants them access");
        (await after.AllowlistedTwitchLogins.AnyAsync(a => a.TwitchLogin == handle.ToLowerInvariant()))
            .Should().BeTrue();
        (await after.EventCompetitors.AnyAsync(c => c.EventId == ev.Id && c.UserId == placeholder.Id))
            .Should().BeTrue();
    }

    [Fact]
    public async Task AddCompetitor_ByAnUnknownHandle_NeedsAnAdminNotJustTheOwner()
    {
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        var ev = new Event { Name = "ev", CreatedById = owner.Id };
        db.Events.Add(ev);
        await db.SaveChangesAsync();

        // Inviting an unknown handle allowlists it, and the allowlist is
        // admin-only — so a non-admin owner may add known users but not mint
        // new ones.
        var result = await AddCompetitorAsync(db, ev, owner, new AddCompetitorRequest(null, "stranger-handle"));

        result.Status().Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task AddCompetitor_ByAnUnknownHandle_AuditsBothTheInviteAndTheAllowlisting()
    {
        var db = CreateDbContext();
        var (admin, ev) = await AddEventAsync(db);
        var handle = $"audited{Guid.NewGuid():N}"[..20];

        await AddCompetitorAsync(db, ev, admin, new AddCompetitorRequest(null, handle));

        var types = await CreateDbContext().AuditLogs
            .Where(a => a.EventId == ev.Id || a.ActorUserId == admin.Id)
            .Select(a => a.Type)
            .ToListAsync();
        types.Should().Contain(AuditEventTypes.CompetitorAdded).And.Contain(AuditEventTypes.AllowlistAdded);
    }

    [Fact]
    public async Task RemoveCompetitor_ByTheOwner_DeletesTheRow()
    {
        var db = CreateDbContext();
        var (admin, ev) = await AddEventAsync(db);
        var target = await AddCompetitorRowAsync(db, ev, "target");

        var result = await RemoveCompetitorAsync(db, ev, target, admin);

        result.Status().Should().Be(StatusCodes.Status204NoContent);
        (await CreateDbContext().EventCompetitors.AnyAsync(c => c.EventId == ev.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task RemoveCompetitor_ByANonOwner_IsForbidden()
    {
        var db = CreateDbContext();
        var (_, ev) = await AddEventAsync(db);
        var target = await AddCompetitorRowAsync(db, ev, "target");
        var stranger = await Fixtures.AddUserAsync(db, "stranger");

        var result = await RemoveCompetitorAsync(db, ev, target, stranger);

        result.Status().Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task RemoveCompetitor_WhoIsNotOnTheRoster_IsNotFound()
    {
        var db = CreateDbContext();
        var (admin, ev) = await AddEventAsync(db);
        var outsider = await Fixtures.AddUserAsync(db, "outsider");

        var result = await RemoveCompetitorAsync(db, ev, outsider, admin);

        result.Status().Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task RemoveCompetitor_TakesTheirModeratorDelegationsWithThem()
    {
        var db = CreateDbContext();
        var (admin, ev) = await AddEventAsync(db);
        var streamer = await AddCompetitorRowAsync(db, ev, "streamer", isStreamer: true);
        var moderator = await Fixtures.AddUserAsync(db, "mod");
        db.EventCompetitorModerators.Add(new EventCompetitorModerator
        {
            EventId = ev.Id,
            CompetitorUserId = streamer.Id,
            ModeratorUserId = moderator.Id,
        });
        await db.SaveChangesAsync();

        var result = await RemoveCompetitorAsync(CreateDbContext(), ev, streamer, admin);

        result.Status().Should().Be(StatusCodes.Status204NoContent);
        (await CreateDbContext().EventCompetitorModerators.AnyAsync(m => m.EventId == ev.Id))
            .Should().BeFalse("a delegation to somebody no longer in the event is meaningless");
    }

    [Fact]
    public async Task AddOrPatchCompetitor_CanSetTheStreamerFlag()
    {
        var db = CreateDbContext();
        var (admin, ev) = await AddEventAsync(db);
        var streamer = await Fixtures.AddUserAsync(db, "streamer");
        var patched = await Fixtures.AddUserAsync(db, "patched");
        db.EventCompetitors.Add(new EventCompetitor { EventId = ev.Id, UserId = patched.Id });
        await db.SaveChangesAsync();

        (await AddCompetitorAsync(db, ev, admin, new AddCompetitorRequest(streamer.Id, null, IsStreamer: true)))
            .Status().Should().Be(StatusCodes.Status201Created);
        (await EventCompetitorsEndpoint.PatchCompetitor(
            ev.Id, patched.Id, new PatchCompetitorRequest(true), admin.Principal(),
            CreateDbContext(), Audit, NullLogger<EventCompetitorsEndpoint>.Instance, default))
            .Status().Should().Be(StatusCodes.Status204NoContent);

        var after = CreateDbContext();
        (await after.EventCompetitors.SingleAsync(c => c.UserId == streamer.Id)).IsStreamer.Should().BeTrue();
        (await after.EventCompetitors.SingleAsync(c => c.UserId == patched.Id)).IsStreamer.Should().BeTrue();
    }

    [Fact]
    public async Task SelfJoin_BeforeTheEventStarts_Succeeds_ButOnlyOnce()
    {
        var db = CreateDbContext();
        var (_, ev) = await AddEventAsync(db);
        var joiner = await Fixtures.AddUserAsync(db, "joiner");

        (await SelfJoinAsync(db, ev, joiner)).Status().Should().Be(StatusCodes.Status201Created);
        (await SelfJoinAsync(CreateDbContext(), ev, joiner)).Status().Should().Be(StatusCodes.Status409Conflict);
        (await CreateDbContext().EventCompetitors.CountAsync(c => c.EventId == ev.Id)).Should().Be(1);
    }

    [Fact]
    public async Task SelfJoin_OnceTheEventHasStarted_Conflicts()
    {
        var db = CreateDbContext();
        var (_, ev) = await AddEventAsync(db, started: true);
        var joiner = await Fixtures.AddUserAsync(db, "joiner");

        var result = await SelfJoinAsync(db, ev, joiner);

        result.Status().Should().Be(StatusCodes.Status409Conflict,
            "joining mid-event would start somebody at an unwinnable deficit");
    }

    [Fact]
    public async Task SelfJoin_OnAnArchivedEvent_IsNotFound()
    {
        var db = CreateDbContext();
        var (_, ev) = await AddEventAsync(db, archived: true);
        var joiner = await Fixtures.AddUserAsync(db, "joiner");

        var result = await SelfJoinAsync(db, ev, joiner);

        result.Status().Should().Be(StatusCodes.Status404NotFound);
    }

    [Theory]
    [InlineData("streamer")]
    [InlineData("admin")]
    public async Task AddModerator_BySomeoneEntitledToDelegate_Succeeds(string caller)
    {
        var db = CreateDbContext();
        var (admin, ev) = await AddEventAsync(db);
        var streamer = await AddCompetitorRowAsync(db, ev, "streamer", isStreamer: true);
        var moderator = await Fixtures.AddUserAsync(db, "mod");

        var result = await AddModeratorAsync(
            db, ev, streamer, moderator, caller == "admin" ? admin : streamer);

        result.Status().Should().Be(StatusCodes.Status201Created);
    }

    [Fact]
    public async Task AddModerator_ByADifferentStreamerInTheSameEvent_IsForbidden()
    {
        var db = CreateDbContext();
        var (_, ev) = await AddEventAsync(db);
        var streamer = await AddCompetitorRowAsync(db, ev, "streamer", isStreamer: true);
        var otherStreamer = await AddCompetitorRowAsync(db, ev, "other", isStreamer: true);
        var moderator = await Fixtures.AddUserAsync(db, "mod");

        var result = await AddModeratorAsync(db, ev, streamer, moderator, otherStreamer);

        result.Status().Should().Be(StatusCodes.Status403Forbidden,
            "delegation is per-streamer, not a shared event-wide list");
    }

    [Fact]
    public async Task AddModerator_WhoIsNotAllowlisted_Conflicts()
    {
        var db = CreateDbContext();
        var (admin, ev) = await AddEventAsync(db);
        var streamer = await AddCompetitorRowAsync(db, ev, "streamer", isStreamer: true);
        var moderator = await Fixtures.AddUserAsync(db, "mod");
        moderator.IsAllowlisted = false;
        await db.SaveChangesAsync();

        var result = await AddModeratorAsync(CreateDbContext(), ev, streamer, moderator, admin);

        result.Status().Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Complete_OnBehalfOfTheStreamerWhoDelegatedYou_RecordsItAgainstThem()
    {
        var db = CreateDbContext();
        var f = await AddDelegatedEventAsync(db);

        var result = await CompleteOnBehalfAsync(CreateDbContext(), f, f.Streamer.Id);

        result.Status().Should().Be(StatusCodes.Status201Created);
        (await CreateDbContext().CompletedObjectives
            .AnyAsync(c => c.ObjectiveId == f.Objective.Id && c.UserId == f.Streamer.Id))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Complete_OnBehalfOfAStreamerWhoDidNotDelegateYou_IsForbidden()
    {
        var db = CreateDbContext();
        var f = await AddDelegatedEventAsync(db);
        var otherStreamer = await AddCompetitorRowAsync(db, f.Event, "other", isStreamer: true);

        var result = await CompleteOnBehalfAsync(CreateDbContext(), f, otherStreamer.Id);

        result.Status().Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Complete_OnBehalfOfACompetitorWhoIsNotAStreamer_IsForbidden()
    {
        var db = CreateDbContext();
        var f = await AddDelegatedEventAsync(db);
        var plainCompetitor = await AddCompetitorRowAsync(db, f.Event, "plain");

        var result = await CompleteOnBehalfAsync(CreateDbContext(), f, plainCompetitor.Id);

        result.Status().Should().Be(StatusCodes.Status403Forbidden,
            "delegation only ever covers a streamer");
    }

    [Fact]
    public async Task Allowlist_IsAdminOnlyToRead()
    {
        var db = CreateDbContext();
        var user = await Fixtures.AddUserAsync(db, "user");

        var result = await AllowlistEndpoint.List(user.Principal(), db, default);

        result.Status().Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Allowlist_StoresAHandleLowercasedAndRefusesADuplicate()
    {
        var db = CreateDbContext();
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);

        var added = await AddAllowlistAsync(db, admin, "Streamer1", "main partner");
        added.Status().Should().Be(StatusCodes.Status201Created);

        var listed = await AllowlistEndpoint.List(admin.Principal(), CreateDbContext(), default);
        listed.Value<List<AllowlistEntryResponse>>()
            .Select(e => e.TwitchLogin).Should().Contain("streamer1");

        var duplicate = await AddAllowlistAsync(CreateDbContext(), admin, "STREAMER1", null);
        duplicate.Status().Should().Be(StatusCodes.Status409Conflict,
            "the unique index is on the lowercased handle, so case is not a way in");
    }

    [Fact]
    public async Task RemovingAnAllowlistEntry_AlsoRevokesTheUsersAccessAndTokens()
    {
        var db = CreateDbContext();
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);
        var target = await Fixtures.AddUserAsync(db, "target");
        var entry = new AllowlistedTwitchLogin { TwitchLogin = target.TwitchLogin };
        db.AllowlistedTwitchLogins.Add(entry);
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = target.Id,
            TokenHash = "h",
            TokenPrefix = "p",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        });
        await db.SaveChangesAsync();

        var result = await AllowlistEndpoint.Remove(
            entry.Id, admin.Principal(), CreateDbContext(), Audit,
            NullLogger<AllowlistEndpoint>.Instance, default);

        result.Status().Should().Be(StatusCodes.Status204NoContent);
        var after = CreateDbContext();
        (await after.Users.SingleAsync(u => u.Id == target.Id)).IsAllowlisted.Should().BeFalse();
        (await after.RefreshTokens.Where(t => t.UserId == target.Id).AllAsync(t => t.IsRevoked))
            .Should().BeTrue("revoking access has to end the sessions it granted");
    }

    [Fact]
    public async Task SetRole_PromotesAUser()
    {
        var db = CreateDbContext();
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);
        var target = await Fixtures.AddUserAsync(db, "target");

        var result = await SetRoleAsync(db, admin, target, nameof(UserRole.Admin));

        result.Status().Should().Be(StatusCodes.Status200OK);
        (await CreateDbContext().Users.SingleAsync(u => u.Id == target.Id)).Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public async Task SetRole_CannotDemoteTheLastAdmin()
    {
        var db = CreateDbContext();
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);

        var result = await SetRoleAsync(db, admin, admin, nameof(UserRole.User));

        result.Status().Should().Be(StatusCodes.Status409Conflict,
            "an instance with no admin left cannot be administered back");
        (await CreateDbContext().Users.SingleAsync(u => u.Id == admin.Id)).Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public async Task SetRole_ToSomethingThatIsNotARole_IsRejected()
    {
        var db = CreateDbContext();
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);
        var target = await Fixtures.AddUserAsync(db, "target");

        var result = await SetRoleAsync(db, admin, target, "Superuser");

        result.Status().Should().Be(StatusCodes.Status400BadRequest);
    }

    private sealed record DelegatedEvent(
        User Admin, User Streamer, User Moderator, Event Event, EventGame Game, Objective Objective);

    private static async Task<(User Admin, Event Event)> AddEventAsync(
        AppDbContext db, bool started = false, bool archived = false)
    {
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);
        var ev = new Event
        {
            Name = "ev",
            CreatedById = admin.Id,
            IsStarted = started,
            IsArchived = archived,
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return (admin, ev);
    }

    private static async Task<User> AddCompetitorRowAsync(
        AppDbContext db, Event ev, string prefix, bool isStreamer = false)
    {
        var user = await Fixtures.AddUserAsync(db, prefix);
        db.EventCompetitors.Add(new EventCompetitor
        {
            EventId = ev.Id,
            UserId = user.Id,
            IsStreamer = isStreamer,
        });
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<DelegatedEvent> AddDelegatedEventAsync(AppDbContext db)
    {
        var (admin, ev) = await AddEventAsync(db, started: true);
        var streamer = await AddCompetitorRowAsync(db, ev, "streamer", isStreamer: true);
        var moderator = await Fixtures.AddUserAsync(db, "mod");
        var game = new EventGame { EventId = ev.Id, KnownGameId = 1, IsEnabled = true };
        db.EventGames.Add(game);
        db.EventCompetitorModerators.Add(new EventCompetitorModerator
        {
            EventId = ev.Id,
            CompetitorUserId = streamer.Id,
            ModeratorUserId = moderator.Id,
        });
        await db.SaveChangesAsync();
        var objective = await Fixtures.AddObjectiveAsync(db, game, "Beat boss");
        return new DelegatedEvent(admin, streamer, moderator, ev, game, objective);
    }

    private Task<IResult> AddCompetitorAsync(
        AppDbContext db, Event ev, User caller, AddCompetitorRequest request) =>
        EventCompetitorsEndpoint.AddCompetitor(
            ev.Id, request, caller.Principal(), db, Audit,
            NullLogger<EventCompetitorsEndpoint>.Instance, Cache, default);

    private Task<IResult> RemoveCompetitorAsync(AppDbContext db, Event ev, User target, User caller) =>
        EventCompetitorsEndpoint.RemoveCompetitor(
            ev.Id, target.Id, caller.Principal(), db, Audit,
            NullLogger<EventCompetitorsEndpoint>.Instance, Cache, default);

    private Task<IResult> SelfJoinAsync(AppDbContext db, Event ev, User caller) =>
        EventCompetitorsEndpoint.SelfJoin(
            ev.Id, caller.Principal(), db, Audit,
            NullLogger<EventCompetitorsEndpoint>.Instance, Cache, default);

    private Task<IResult> AddModeratorAsync(
        AppDbContext db, Event ev, User streamer, User moderator, User caller) =>
        EventCompetitorModeratorsEndpoint.Add(
            ev.Id, streamer.Id, new AddModeratorRequest(moderator.Id), caller.Principal(),
            db, Audit, NullLogger<EventCompetitorModeratorsEndpoint>.Instance, default);

    private Task<IResult> CompleteOnBehalfAsync(AppDbContext db, DelegatedEvent f, Guid onBehalfOfUserId) =>
        CompletedObjectivesEndpoint.CompleteObjective(
            f.Event.Id, f.Game.Id, f.Objective.Id, f.Moderator.Principal(),
            db, Audit, Cache, NullLogger<CompletedObjectivesEndpoint>.Instance, default,
            onBehalfOfUserId: onBehalfOfUserId);

    private Task<IResult> AddAllowlistAsync(AppDbContext db, User admin, string handle, string? note) =>
        AllowlistEndpoint.Add(
            new AddAllowlistRequest(handle, note), admin.Principal(), db, Audit,
            NullLogger<AllowlistEndpoint>.Instance, default);

    private Task<IResult> SetRoleAsync(AppDbContext db, User admin, User target, string role) =>
        AdminUsersEndpoint.SetRole(
            target.Id, new SetRoleRequest(role), admin.Principal(), db, Audit,
            NullLogger<AdminUsersEndpoint>.Instance, default);
}
