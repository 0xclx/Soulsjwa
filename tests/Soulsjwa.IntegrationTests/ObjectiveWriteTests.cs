using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Soulsjwa.Api.Features.Events.Endpoints;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// Who may record an objective write, against which event and game, and what a
/// second write does. Enforced partly by Postgres — the completion runs in a
/// transaction and lands under a partial unique index. The wire contract stays
/// in <c>Soulsjwa.ApiTests.CompletedObjectivesEndpointTests</c>.
/// </summary>
public class ObjectiveWriteTests : IntegrationTestBase
{
    [Fact]
    public async Task Complete_AsACompetitor_RecordsAnOfficialCompletion()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, score: 25);

        var result = await CompleteAsync(db, f, f.Competitor.Id);

        result.Status().Should().Be(StatusCodes.Status201Created);
        var completion = await CreateDbContext().CompletedObjectives
            .SingleAsync(c => c.ObjectiveId == f.Objective.Id && c.UserId == f.Competitor.Id);
        completion.TrialRunId.Should().BeNull("no trial run means the write is official");
    }

    [Fact]
    public async Task Complete_ByANonCompetitor_IsForbidden()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        var stranger = await Fixtures.AddUserAsync(db, "stranger");

        var result = await CompleteAsync(db, f, stranger.Id, caller: stranger);

        result.Status().Should().Be(StatusCodes.Status403Forbidden);
        result.Detail().Should().Contain("not a competitor");
    }

    [Fact]
    public async Task Complete_Twice_Conflicts_AndLeavesOneRow()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        (await CompleteAsync(db, f, f.Competitor.Id)).Status().Should().Be(StatusCodes.Status201Created);

        var duplicate = await CompleteAsync(CreateDbContext(), f, f.Competitor.Id);

        duplicate.Status().Should().Be(StatusCodes.Status409Conflict);
        (await CreateDbContext().CompletedObjectives
            .CountAsync(c => c.ObjectiveId == f.Objective.Id && c.UserId == f.Competitor.Id))
            .Should().Be(1);
    }

    [Fact]
    public async Task Complete_AnObjectiveOfAnotherGame_IsNotFound()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        var otherGame = new EventGame { EventId = f.Event.Id, CustomGameName = "Other" };
        db.EventGames.Add(otherGame);
        await db.SaveChangesAsync();
        var strayObjective = await Fixtures.AddObjectiveAsync(db, otherGame);

        // The objective exists, but not under the game named in the call —
        // the pair has to be validated together, not each on its own.
        var result = await CompletedObjectivesEndpoint.CompleteObjective(
            f.Event.Id, f.Game.Id, strayObjective.Id, f.Competitor.Principal(),
            db, Audit, Cache, NullLogger<CompletedObjectivesEndpoint>.Instance, default);

        result.Status().Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Complete_AnUnknownObjective_IsNotFound()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);

        var result = await CompletedObjectivesEndpoint.CompleteObjective(
            f.Event.Id, f.Game.Id, Guid.NewGuid(), f.Competitor.Principal(),
            db, Audit, Cache, NullLogger<CompletedObjectivesEndpoint>.Instance, default);

        result.Status().Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Complete_BeforeTheEventStarts_IsForbidden()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, started: false);

        var result = await CompleteAsync(db, f, f.Competitor.Id);

        result.Status().Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Complete_OnAGameThatIsNotEnabled_IsForbidden()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, enabled: false);

        var result = await CompleteAsync(db, f, f.Competitor.Id);

        result.Status().Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Uncomplete_AfterACompletion_RemovesTheRow()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        await CompleteAsync(db, f, f.Competitor.Id);

        var result = await CompletedObjectivesEndpoint.UncompleteObjective(
            f.Event.Id, f.Game.Id, f.Objective.Id, f.Competitor.Principal(),
            CreateDbContext(), Audit, Cache, NullLogger<CompletedObjectivesEndpoint>.Instance, default);

        result.Status().Should().Be(StatusCodes.Status204NoContent);
        (await CreateDbContext().CompletedObjectives
            .AnyAsync(c => c.ObjectiveId == f.Objective.Id && c.UserId == f.Competitor.Id))
            .Should().BeFalse();
    }

    [Fact]
    public async Task CompleteAndFail_Concurrently_RecordOneOutcomeBetweenThem()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);

        // Two contexts, genuinely in parallel: the mutual exclusion is held by
        // a transaction plus the partial unique indexes, neither of which
        // exists on one change tracker or a fake provider.
        var completeDb = CreateDbContext();
        var failDb = CreateDbContext();
        var results = await Task.WhenAll(
            CompletedObjectivesEndpoint.CompleteObjective(
                f.Event.Id, f.Game.Id, f.Objective.Id, f.Competitor.Principal(),
                completeDb, Audit, Cache, NullLogger<CompletedObjectivesEndpoint>.Instance, default),
            FailedObjectivesEndpoint.FailObjective(
                f.Event.Id, f.Game.Id, f.Objective.Id, f.Competitor.Principal(),
                failDb, Audit, Cache, NullLogger<FailedObjectivesEndpoint>.Instance, default));

        results.Select(r => r.Status()).Should().BeEquivalentTo(
            [StatusCodes.Status201Created, StatusCodes.Status409Conflict],
            "exactly one of the two writes must win");

        var verify = CreateDbContext();
        var completed = await verify.CompletedObjectives
            .CountAsync(c => c.ObjectiveId == f.Objective.Id && c.UserId == f.Competitor.Id);
        var failed = await verify.FailedObjectives
            .CountAsync(x => x.ObjectiveId == f.Objective.Id && x.UserId == f.Competitor.Id);
        (completed + failed).Should().Be(1, "an objective is completed or failed, never both");
    }

    [Fact]
    public async Task EditCompletionTime_WithAnOffsetInstant_StoresThatInstantInUtc()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        await CompleteAsync(db, f, f.Competitor.Id);

        // The same moment expressed in +02:00. DateTime.SpecifyKind(…, Utc)
        // would have stamped the wall-clock reading as UTC instead — a
        // two-hour shift, and the bug this guards.
        // Ahead of now, not behind it: the handler's lower bound is the
        // event's CreatedAt, which this fixture set moments ago. The upper
        // bound is now + 1 minute.
        var instant = DateTime.UtcNow.AddSeconds(5);
        var offsetForm = new DateTimeOffset(
            DateTime.SpecifyKind(instant.AddHours(2), DateTimeKind.Unspecified), TimeSpan.FromHours(2));

        var result = await CompletedObjectivesEndpoint.EditCompletionTime(
            f.Event.Id, f.Game.Id, f.Objective.Id, f.Competitor.Id,
            new EditCompletionTimeRequest(offsetForm, "backfilled from stream VOD"),
            f.Owner.Principal(), CreateDbContext(), Audit, Cache,
            NullLogger<CompletedObjectivesEndpoint>.Instance, default);

        result.Status().Should().Be(StatusCodes.Status200OK);
        var completion = await CreateDbContext().CompletedObjectives
            .SingleAsync(c => c.ObjectiveId == f.Objective.Id && c.UserId == f.Competitor.Id);
        completion.CompletedAt.Should().BeCloseTo(
            DateTime.SpecifyKind(instant, DateTimeKind.Utc), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task EditCompletionTime_ByACompetitor_IsForbidden()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        await CompleteAsync(db, f, f.Competitor.Id);

        var result = await CompletedObjectivesEndpoint.EditCompletionTime(
            f.Event.Id, f.Game.Id, f.Objective.Id, f.Competitor.Id,
            new EditCompletionTimeRequest(DateTimeOffset.UtcNow, "because I say so"),
            f.Competitor.Principal(), CreateDbContext(), Audit, Cache,
            NullLogger<CompletedObjectivesEndpoint>.Instance, default);

        result.Status().Should().Be(StatusCodes.Status403Forbidden,
            "editing a recorded time is the owner's or an admin's call, not a competitor's");
    }

    [Fact]
    public async Task EditCompletionTime_WithNoReason_IsAValidationProblem()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        await CompleteAsync(db, f, f.Competitor.Id);

        var result = await CompletedObjectivesEndpoint.EditCompletionTime(
            f.Event.Id, f.Game.Id, f.Objective.Id, f.Competitor.Id,
            new EditCompletionTimeRequest(DateTimeOffset.UtcNow, "  "),
            f.Owner.Principal(), CreateDbContext(), Audit, Cache,
            NullLogger<CompletedObjectivesEndpoint>.Instance, default);

        result.Status().Should().Be(StatusCodes.Status400BadRequest);
        result.ValidationErrors().Should().ContainKey(nameof(EditCompletionTimeRequest.Reason));
    }

    [Fact]
    public async Task GetScores_AggregatesPerCompetitor()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, score: 25);
        await CompleteAsync(db, f, f.Competitor.Id);

        var result = await CompletedObjectivesEndpoint.GetScores(f.Event.Id, CreateDbContext(), default);

        result.Value<IEnumerable<ScoreEntry>>().Should().ContainSingle(e =>
            e.UserId == f.Competitor.Id && e.TotalScore == 25 && e.CompletedCount == 1);
    }

    [Fact]
    public async Task GetScores_ForAnUnknownEvent_IsNotFound()
    {
        var result = await CompletedObjectivesEndpoint.GetScores(Guid.NewGuid(), CreateDbContext(), default);

        result.Status().Should().Be(StatusCodes.Status404NotFound);
    }

    private Task<IResult> CompleteAsync(
        AppDbContext db, EventFixture fixture, Guid targetUserId, Soulsjwa.Api.Features.Auth.Entities.User? caller = null) =>
        CompletedObjectivesEndpoint.CompleteObjective(
            fixture.Event.Id,
            fixture.Game.Id,
            fixture.Objective.Id,
            (caller ?? fixture.Competitor).Principal(),
            db,
            Audit,
            Cache,
            NullLogger<CompletedObjectivesEndpoint>.Instance,
            default,
            onBehalfOfUserId: targetUserId == (caller ?? fixture.Competitor).Id ? null : targetUserId);
}
