using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Soulsjwa.Api.Common.Models;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events.Endpoints;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// Listing, identifier resolution, editing, and the archive / start-stop /
/// featured lifecycle. Postgres is load-bearing throughout: the alias and
/// featured constraints are unique indexes, the search is <c>ILIKE</c> against
/// trigram indexes, archived-visibility is a query filter bypassed with
/// <c>IgnoreQueryFilters</c>, and the list's fixed query count only exists as
/// SQL. The wire contract is <c>Soulsjwa.ApiTests.EventsEndpointTests</c>.
/// </summary>
public class EventLifecycleTests : IntegrationTestBase
{
    [Theory]
    [InlineData(1, 99999, 1, 100)]
    [InlineData(-5, 20, 1, 20)]
    [InlineData(0, 0, 1, 1)]
    public async Task ListEvents_ClampsPageAndPageSize(
        int page, int pageSize, int expectedPage, int expectedPageSize)
    {
        var result = await ListAsync(page: page, pageSize: pageSize);

        var body = result.Value<PaginatedResponse<EventListItemResponse>>();
        body.Page.Should().Be(expectedPage);
        body.PageSize.Should().Be(expectedPageSize);
    }

    [Fact]
    public async Task ListEvents_LeavesArchivedEventsOut()
    {
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        await AddEventsAsync(db, owner, ("active", false), ("archived", true));

        var names = await ListedNamesAsync();

        names.Should().Contain("active").And.NotContain("archived");
    }

    [Fact]
    public async Task ListEvents_IncludeArchived_ShowsThemToAnAdmin()
    {
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);
        await AddEventsAsync(db, owner, ("still-here", true));

        var names = await ListedNamesAsync(admin, includeArchived: true);

        names.Should().Contain("still-here");
    }

    [Fact]
    public async Task ListEvents_IncludeArchived_ShowsThemToNobodyAnonymous()
    {
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        await AddEventsAsync(db, owner, ("hidden-from-anon", true));

        var names = await ListedNamesAsync(includeArchived: true);

        names.Should().NotContain("hidden-from-anon",
            "includeArchived is a filter, not an authorization bypass");
    }

    [Fact]
    public async Task ListEvents_IncludeArchived_ShowsAPlainUserOnlyTheirOwn()
    {
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        var other = await Fixtures.AddUserAsync(db, "other");
        await AddEventsAsync(db, owner, ("not-mine-archived", true));
        await AddEventsAsync(db, other, ("mine-archived", true));

        var names = await ListedNamesAsync(other, includeArchived: true);

        names.Should().Contain("mine-archived").And.NotContain("not-mine-archived");
    }

    [Fact]
    public async Task ListEvents_StatusArchived_ReturnsOnlyArchived()
    {
        var db = CreateDbContext();
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);
        await AddEventsAsync(db, admin, ("active-status", false), ("archived-status", true));

        var result = await ListAsync(admin, status: "archived");

        var items = result.Value<PaginatedResponse<EventListItemResponse>>().Items;
        items.Should().OnlyContain(i => i.IsArchived);
        items.Select(i => i.Name).Should().Contain("archived-status");
    }

    [Fact]
    public async Task ListEvents_Search_MatchesNameOrDescription()
    {
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        db.Events.Add(new Event { Name = "Needle Run", Description = "Haligtree", CreatedById = owner.Id });
        db.Events.Add(new Event { Name = "Bonfire Bash", Description = "Firelink", CreatedById = owner.Id });
        await db.SaveChangesAsync();

        var names = await ListedNamesAsync(search: "needle");

        names.Should().Contain("Needle Run").And.NotContain("Bonfire Bash");
    }

    [Theory]
    [InlineData("_")]
    [InlineData("%")]
    [InlineData("100%")]
    [InlineData("a_b")]
    [InlineData("back\\slash")]
    public async Task ListEvents_Search_TreatsLikeWildcardsAsLiterals(string needle)
    {
        // "_" and "%" are LIKE wildcards; typed by a user they are just text.
        // Unescaped, a search for "_" matched every event with a name.
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        db.Events.Add(new Event { Name = $"has {needle} in it", Description = "", CreatedById = owner.Id });
        db.Events.Add(new Event { Name = "plain name", Description = "plain description", CreatedById = owner.Id });
        await db.SaveChangesAsync();

        var names = await ListedNamesAsync(search: needle);

        names.Should().ContainSingle().Which.Should().Be($"has {needle} in it");
    }

    [Fact]
    public async Task ListEvents_Search_IsCaseInsensitiveAcrossTheDescription()
    {
        // Pins that case-insensitive search survived the switch from
        // ToLower().Contains() to EF.Functions.ILike. A property of the SQL, so
        // nothing short of real Postgres can show it.
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        db.Events.Add(new Event { Name = "Speedrun", Description = "Race through Haligtree Canopy", CreatedById = owner.Id });
        db.Events.Add(new Event { Name = "Other", Description = "Unrelated", CreatedById = owner.Id });
        await db.SaveChangesAsync();

        var names = await ListedNamesAsync(search: "HALIGTREE");

        names.Should().ContainSingle().Which.Should().Be("Speedrun");
    }

    [Fact]
    public async Task ListEvents_IssuesTheSameNumberOfQueriesWhateverTheEventCount()
    {
        // Include-based mapping issued one round trip per Include chain (five
        // in total). The projection must issue exactly one query for the page
        // plus one for the count, however many events, competitors or games
        // exist.
        var seed = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(seed, "owner");
        await AddEventWithGraphAsync(seed, owner, 2);

        var interceptor = new CommandCountingInterceptor();
        var db = CreateDbContext(interceptor);

        await ListEventsEndpointAsync(db, owner, pageSize: 100);
        var atTwoEvents = interceptor.Count;

        await AddEventWithGraphAsync(CreateDbContext(), owner, 8);
        interceptor.Reset();

        await ListEventsEndpointAsync(db, owner, pageSize: 100);

        interceptor.Count.Should().Be(atTwoEvents,
            "the number of SQL commands must not grow with the number of events");
    }

    [Fact]
    public async Task GetEvent_ByUrlAlias_ResolvesToTheSameEventAsById()
    {
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        var ev = new Event { Name = "aliased", UrlAlias = "summer-race", CreatedById = owner.Id };
        db.Events.Add(ev);
        await db.SaveChangesAsync();

        var byId = await GetAsync(ev.Id.ToString());
        var byAlias = await GetAsync("summer-race");

        byAlias.Value<EventResponse>().Should().BeEquivalentTo(byId.Value<EventResponse>());
        byAlias.Value<EventResponse>().UrlAlias.Should().Be("summer-race");
    }

    [Fact]
    public async Task GetEvent_Archived_LooksExactlyLikeAMissingEventToAnonymous()
    {
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        var archived = await AddEventsAsync(db, owner, ("archived-anon", true));

        var archivedResult = await GetAsync(archived[0].Id.ToString());
        var missingResult = await GetAsync(Guid.NewGuid().ToString());

        archivedResult.Status().Should().Be(StatusCodes.Status404NotFound);
        missingResult.Status().Should().Be(StatusCodes.Status404NotFound);
        archivedResult.Detail().Should().Be(missingResult.Detail(),
            "a 403 — or a different message — would confirm the event exists");
    }

    [Fact]
    public async Task GetEvent_Archived_IsNotFoundForAnAuthenticatedNonMember()
    {
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        var stranger = await Fixtures.AddUserAsync(db, "stranger");
        var archived = await AddEventsAsync(db, owner, ("archived-nonmember", true));

        var result = await GetAsync(archived[0].Id.ToString(), stranger);

        result.Status().Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetEvent_Archived_IsVisibleToItsCompetitor()
    {
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        var competitor = await Fixtures.AddUserAsync(db, "competitor");
        var archived = await AddEventsAsync(db, owner, ("archived-competitor", true));
        db.EventCompetitors.Add(new EventCompetitor { EventId = archived[0].Id, UserId = competitor.Id });
        await db.SaveChangesAsync();

        var result = await GetAsync(archived[0].Id.ToString(), competitor);

        result.Status().Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task GetEvent_Archived_IsVisibleToAnAdmin()
    {
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);
        var archived = await AddEventsAsync(db, owner, ("archived-admin", true));

        var result = await GetAsync(archived[0].Id.ToString(), admin);

        result.Status().Should().Be(StatusCodes.Status200OK);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateEvent_WithoutARealName_IsRejected(string name)
    {
        var db = CreateDbContext();
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);

        var result = await EventsEndpoint.CreateEvent(
            new CreateEventRequest(name, ""), admin.Principal(), db, Audit,
            NullLogger<EventsEndpoint>.Instance, default);

        result.Status().Should().Be(StatusCodes.Status400BadRequest);
        result.ValidationErrors().Should().ContainKey("Name");
    }

    [Fact]
    public async Task CreateEvent_WithAnOverlongName_IsRejected()
    {
        var db = CreateDbContext();
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);

        var result = await EventsEndpoint.CreateEvent(
            new CreateEventRequest(new string('x', 201), ""), admin.Principal(), db, Audit,
            NullLogger<EventsEndpoint>.Instance, default);

        result.Status().Should().Be(StatusCodes.Status400BadRequest);
        result.ValidationErrors().Should().ContainKey("Name");
    }

    [Fact]
    public async Task PatchEvent_ThatDoesNotExist_IsNotFound()
    {
        var db = CreateDbContext();
        var user = await Fixtures.AddUserAsync(db, "user", UserRole.Admin);

        var result = await PatchAsync(db, Guid.NewGuid(), user, new PatchEventRequest("x", null, null, null, null));

        result.Status().Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task PatchEvent_WithABlankName_IsRejected()
    {
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        var ev = (await AddEventsAsync(db, owner, ("x", false)))[0];

        var result = await PatchAsync(db, ev.Id, owner, new PatchEventRequest("   ", null, null, null, null));

        result.Status().Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task PatchEvent_CanSetChangeAndClearTheUrlAlias()
    {
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        var ev = (await AddEventsAsync(db, owner, ("alias edits", false)))[0];

        var set = await PatchAsync(CreateDbContext(), ev.Id, owner, AliasPatch("summer-race"));
        set.Value<EventResponse>().UrlAlias.Should().Be("summer-race");

        var changed = await PatchAsync(CreateDbContext(), ev.Id, owner, AliasPatch("winter-race"));
        changed.Value<EventResponse>().UrlAlias.Should().Be("winter-race");
        (await GetAsync("summer-race")).Status().Should().Be(StatusCodes.Status404NotFound,
            "the old alias must stop resolving");

        var cleared = await PatchAsync(CreateDbContext(), ev.Id, owner, AliasPatch(""));
        cleared.Value<EventResponse>().UrlAlias.Should().BeNull();
        (await GetAsync("winter-race")).Status().Should().Be(StatusCodes.Status404NotFound);
        (await GetAsync(ev.Id.ToString())).Status().Should().Be(StatusCodes.Status200OK,
            "clearing the alias must not make the event unreachable by id");
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("Summer-Race")]
    [InlineData("summer--race")]
    [InlineData("-summer-race")]
    [InlineData("summer_race")]
    [InlineData("11111111-1111-1111-1111-111111111111")]
    public async Task PatchEvent_WithAnUnusableUrlAlias_IsRejected(string urlAlias)
    {
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        var ev = (await AddEventsAsync(db, owner, ("invalid alias", false)))[0];

        var result = await PatchAsync(db, ev.Id, owner, AliasPatch(urlAlias));

        result.Status().Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task PatchEvent_WithAnAliasAnotherEventHolds_Conflicts()
    {
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        db.Events.Add(new Event { Name = "first", UrlAlias = "shared-alias", CreatedById = owner.Id });
        var second = new Event { Name = "second", CreatedById = owner.Id };
        db.Events.Add(second);
        await db.SaveChangesAsync();

        var result = await PatchAsync(CreateDbContext(), second.Id, owner, AliasPatch("shared-alias"));

        result.Status().Should().Be(StatusCodes.Status409Conflict,
            "IX_Events_UrlAlias is unique, and the handler must turn that into a 409 rather than a 500");
    }

    [Fact]
    public async Task PatchEvent_ByAnAdminWhoDoesNotOwnIt_IsAllowed()
    {
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);
        var ev = (await AddEventsAsync(db, owner, ("admin alias", false)))[0];

        var result = await PatchAsync(db, ev.Id, admin, AliasPatch("admin-defined"));

        result.Status().Should().Be(StatusCodes.Status200OK);
        result.Value<EventResponse>().UrlAlias.Should().Be("admin-defined");
    }

    [Fact]
    public async Task PatchEvent_TieBreakMode_DefaultsToSharedPlaceAndCanBeSetToByTime()
    {
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        var ev = (await AddEventsAsync(db, owner, ("tiebreak", false)))[0];

        (await GetAsync(ev.Id.ToString())).Value<EventResponse>()
            .TieBreakMode.Should().Be(nameof(TieBreakMode.SharedPlace),
                "a fresh event shares placings on an equal score");

        var result = await PatchAsync(
            db, ev.Id, owner, new PatchEventRequest(null, null, nameof(TieBreakMode.ByTime), null, null));

        result.Status().Should().Be(StatusCodes.Status200OK);
        result.Value<EventResponse>().TieBreakMode.Should().Be(nameof(TieBreakMode.ByTime));
    }

    [Fact]
    public async Task PatchEvent_WithAnUnknownTieBreakMode_IsRejected()
    {
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        var ev = (await AddEventsAsync(db, owner, ("tb-invalid", false)))[0];

        var result = await PatchAsync(db, ev.Id, owner, new PatchEventRequest(null, null, "ByVibes", null, null));

        result.Status().Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task StartThenStop_FlipsTheRunningFlagBothWays()
    {
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        var ev = (await AddEventsAsync(db, owner, ("to-start", false)))[0];

        (await StartAsync(db, ev.Id, owner)).Status().Should().Be(StatusCodes.Status204NoContent);
        (await GetAsync(ev.Id.ToString())).Value<EventResponse>().IsStarted.Should().BeTrue();

        (await StopAsync(CreateDbContext(), ev.Id, owner)).Status().Should().Be(StatusCodes.Status204NoContent);
        (await GetAsync(ev.Id.ToString())).Value<EventResponse>().IsStarted.Should().BeFalse();
    }

    [Fact]
    public async Task Stop_WhileAGameIsStillEnabled_Conflicts()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, enabled: true);

        var result = await StopAsync(db, f.Event.Id, f.Owner);

        result.Status().Should().Be(StatusCodes.Status409Conflict,
            "stopping with a game still active would leave competitors able to score");
    }

    [Fact]
    public async Task Stop_OnceEveryGameIsDisabled_Succeeds()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, enabled: false);

        var result = await StopAsync(db, f.Event.Id, f.Owner);

        result.Status().Should().Be(StatusCodes.Status204NoContent);
    }

    [Fact]
    public async Task Archive_HidesTheEventFromTheListButNotFromItsOwner()
    {
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        var ev = (await AddEventsAsync(db, owner, ("to-archive", false)))[0];

        var result = await EventsEndpoint.ArchiveEvent(
            ev.Id, owner.Principal(), db, Audit, NullLogger<EventsEndpoint>.Instance, default);

        result.Status().Should().Be(StatusCodes.Status204NoContent);
        (await ListedNamesAsync()).Should().NotContain("to-archive");
        (await GetAsync(ev.Id.ToString(), owner)).Status().Should().Be(StatusCodes.Status200OK,
            "an owner still needs the row to inspect and restore it");
    }

    [Fact]
    public async Task Unarchive_PutsItBackInTheList()
    {
        var db = CreateDbContext();
        var owner = await Fixtures.AddUserAsync(db, "owner");
        var ev = (await AddEventsAsync(db, owner, ("to-restore", true)))[0];

        var result = await EventsEndpoint.UnarchiveEvent(
            ev.Id, owner.Principal(), db, Audit, NullLogger<EventsEndpoint>.Instance, default);

        result.Status().Should().Be(StatusCodes.Status204NoContent);
        (await ListedNamesAsync()).Should().Contain("to-restore");
    }

    [Fact]
    public async Task Feature_ASecondEvent_UnfeaturesTheFirst()
    {
        var db = CreateDbContext();
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);
        var events = await AddEventsAsync(db, admin, ("first-featured", false), ("second-featured", false));

        (await FeatureAsync(CreateDbContext(), events[0].Id, admin)).Status()
            .Should().Be(StatusCodes.Status204NoContent);
        (await FeatureAsync(CreateDbContext(), events[1].Id, admin)).Status()
            .Should().Be(StatusCodes.Status204NoContent);

        (await GetAsync(events[0].Id.ToString())).Value<EventResponse>().IsFeatured.Should().BeFalse();
        (await GetAsync(events[1].Id.ToString())).Value<EventResponse>().IsFeatured.Should().BeTrue();
    }

    [Fact]
    public async Task Feature_AnAlreadyFeaturedEvent_IsANoOp()
    {
        var db = CreateDbContext();
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);
        var ev = (await AddEventsAsync(db, admin, ("already-featured", false)))[0];

        (await FeatureAsync(CreateDbContext(), ev.Id, admin)).Status().Should().Be(StatusCodes.Status204NoContent);
        (await FeatureAsync(CreateDbContext(), ev.Id, admin)).Status().Should().Be(StatusCodes.Status204NoContent);

        (await GetAsync(ev.Id.ToString())).Value<EventResponse>().IsFeatured.Should().BeTrue();
    }

    [Fact]
    public async Task Unfeature_ClearsTheFlag()
    {
        var db = CreateDbContext();
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);
        var ev = (await AddEventsAsync(db, admin, ("to-unfeature", false)))[0];
        await FeatureAsync(CreateDbContext(), ev.Id, admin);

        var result = await EventsEndpoint.UnfeatureEvent(
            ev.Id, admin.Principal(), CreateDbContext(), Audit, NullLogger<EventsEndpoint>.Instance, default);

        result.Status().Should().Be(StatusCodes.Status204NoContent);
        (await GetAsync(ev.Id.ToString())).Value<EventResponse>().IsFeatured.Should().BeFalse();
    }

    [Fact]
    public async Task Feature_TwoEventsConcurrently_LeavesExactlyOneFeatured()
    {
        // IX_Events_FeaturedEvent is the arbiter under a race — the loser must
        // get a 409, not a silent second featured row or a 500.
        var db = CreateDbContext();
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);
        var events = await AddEventsAsync(db, admin, ("race-a", false), ("race-b", false));

        var results = await Task.WhenAll(
            FeatureAsync(CreateDbContext(), events[0].Id, admin),
            FeatureAsync(CreateDbContext(), events[1].Id, admin));

        results.Should().Contain(r => r.Status() == StatusCodes.Status204NoContent);
        results.Select(r => r.Status()).Should().OnlyContain(
            status => status == StatusCodes.Status204NoContent || status == StatusCodes.Status409Conflict);
        (await CreateDbContext().Events.CountAsync(e => e.IsFeatured)).Should().Be(1);
    }

    [Fact]
    public async Task GetFeatured_WithNothingFeatured_IsNotFound()
    {
        var result = await EventsEndpoint.GetFeaturedEvent(CreateDbContext(), default);

        result.Status().Should().Be(StatusCodes.Status404NotFound);
    }

    private static PatchEventRequest AliasPatch(string alias) => new(null, null, null, alias, null);

    private static async Task<List<Event>> AddEventsAsync(
        AppDbContext db, User owner, params (string Name, bool Archived)[] events)
    {
        var rows = events.Select(e => new Event
        {
            Name = e.Name,
            CreatedById = owner.Id,
            IsArchived = e.Archived,
        }).ToList();
        db.Events.AddRange(rows);
        await db.SaveChangesAsync();
        return rows;
    }

    /// <summary>
    /// <paramref name="count"/> events, each with a game, an objective and a
    /// competitor — the graph the list projection must not walk per row.
    /// </summary>
    private static async Task AddEventWithGraphAsync(AppDbContext db, User owner, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var ev = new Event { Name = $"Event {Guid.NewGuid():N}", CreatedById = owner.Id };
            var game = new EventGame { Event = ev, CustomGameName = "Game" };
            db.AddRange(
                ev,
                game,
                new Objective { EventGame = game, Name = "Obj", Score = 10 },
                new EventCompetitor { Event = ev, UserId = owner.Id });
        }
        await db.SaveChangesAsync();
    }

    private Task<IResult> ListAsync(
        User? caller = null,
        int page = 1,
        int pageSize = 100,
        bool includeArchived = false,
        string? search = null,
        string? status = null) =>
        ListEventsEndpointAsync(CreateDbContext(), caller, page, pageSize, includeArchived, search, status);

    private static Task<IResult> ListEventsEndpointAsync(
        AppDbContext db,
        User? caller = null,
        int page = 1,
        int pageSize = 100,
        bool includeArchived = false,
        string? search = null,
        string? status = null) =>
        EventsEndpoint.ListEvents(
            caller?.Principal() ?? HandlerHarness.Anonymous(), db, default,
            page, pageSize, includeArchived, search, status ?? "all");

    private async Task<IEnumerable<string>> ListedNamesAsync(
        User? caller = null, bool includeArchived = false, string? search = null)
    {
        var result = await ListAsync(caller, includeArchived: includeArchived, search: search);
        return result.Value<PaginatedResponse<EventListItemResponse>>().Items.Select(i => i.Name);
    }

    private Task<IResult> GetAsync(string identifier, User? caller = null) =>
        EventsEndpoint.GetEvent(
            identifier, caller?.Principal() ?? HandlerHarness.Anonymous(), CreateDbContext(), default);

    private Task<IResult> PatchAsync(AppDbContext db, Guid eventId, User caller, PatchEventRequest request) =>
        EventsEndpoint.PatchEvent(
            eventId, request, caller.Principal(), db, Audit, NullLogger<EventsEndpoint>.Instance, default);

    private Task<IResult> StartAsync(AppDbContext db, Guid eventId, User caller) =>
        EventsEndpoint.StartEvent(
            eventId, caller.Principal(), db, Audit, NullLogger<EventsEndpoint>.Instance, default);

    private Task<IResult> StopAsync(AppDbContext db, Guid eventId, User caller) =>
        EventsEndpoint.StopEvent(
            eventId, caller.Principal(), db, Audit, NullLogger<EventsEndpoint>.Instance, default);

    private Task<IResult> FeatureAsync(AppDbContext db, Guid eventId, User caller) =>
        EventsEndpoint.FeatureEvent(
            eventId, caller.Principal(), db, Audit, NullLogger<EventsEndpoint>.Instance, default);
}
