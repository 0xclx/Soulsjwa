using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Soulsjwa.Api.Common;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events.Endpoints;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// Failing the rest of a game in one go: which objectives it touches, which it
/// leaves alone, whose record it lands on, and who may do it. The wire contract
/// stays in <c>Soulsjwa.ApiTests.CompletedObjectivesEndpointTests</c>.
/// </summary>
public class FailRemainingObjectivesTests : IntegrationTestBase
{
    [Fact]
    public async Task FailRemaining_FailsOnlyThePendingObjectives_AndFinishesTheCompetitor()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        var completed = await Fixtures.AddObjectiveAsync(db, f.Game, "done");
        var failed = await Fixtures.AddObjectiveAsync(db, f.Game, "lost");
        db.CompletedObjectives.Add(new CompletedObjective { ObjectiveId = completed.Id, UserId = f.Competitor.Id });
        db.FailedObjectives.Add(new FailedObjective { ObjectiveId = failed.Id, UserId = f.Competitor.Id });
        await db.SaveChangesAsync();

        var result = await FailRemainingAsync(CreateDbContext(), f);

        result.Status().Should().Be(StatusCodes.Status200OK);
        var body = result.Value<FailRemainingObjectivesResponse>();
        body.Should().Be(new FailRemainingObjectivesResponse(
            FailedCount: 1, AlreadyCompletedCount: 1, AlreadyFailedCount: 1, TotalObjectives: 3));

        var verify = CreateDbContext();
        (await verify.FailedObjectives.Where(x => x.UserId == f.Competitor.Id).Select(x => x.ObjectiveId).ToListAsync())
            .Should().BeEquivalentTo([failed.Id, f.Objective.Id], "the completed objective stays completed");
        (await verify.CompletedObjectives.CountAsync(c => c.UserId == f.Competitor.Id)).Should().Be(1);

        var scoreboard = await ScoreboardEndpoint.BuildAsync(f.Event.Id, verify, default);
        var entry = scoreboard!.Entries.Single(e => e.UserId == f.Competitor.Id);
        entry.IsFinished.Should().BeTrue("every objective is now completed or failed");
        entry.Status.Should().Be(nameof(ObjectiveOutcome.Failed));

        Cache.Evicted.Should().Contain(CacheTags.Scoreboard(f.Event.Id));
        var auditRow = await verify.AuditLogs.SingleAsync(a =>
            a.EventId == f.Event.Id && a.Type == AuditEventTypes.ObjectivesRemainingFailed);
        auditRow.EventGameId.Should().Be(f.Game.Id);
        auditRow.SubjectUserId.Should().Be(f.Competitor.Id);
    }

    [Fact]
    public async Task FailRemaining_WhenNothingIsPending_ChangesNothing()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        (await FailRemainingAsync(db, f)).Status().Should().Be(StatusCodes.Status200OK);

        var again = await FailRemainingAsync(CreateDbContext(), f);

        again.Status().Should().Be(StatusCodes.Status200OK);
        again.Value<FailRemainingObjectivesResponse>().FailedCount.Should().Be(0);
        (await CreateDbContext().FailedObjectives.CountAsync(x => x.UserId == f.Competitor.Id)).Should().Be(1);
    }

    [Fact]
    public async Task FailRemaining_ByANonCompetitor_IsForbidden()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        var stranger = await Fixtures.AddUserAsync(db, "stranger");

        var result = await FailRemainingAsync(db, f, caller: stranger);

        result.Status().Should().Be(StatusCodes.Status403Forbidden);
        (await CreateDbContext().FailedObjectives.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task FailRemaining_OnAGameThatIsNotEnabled_IsForbidden()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, enabled: false);

        var result = await FailRemainingAsync(db, f);

        result.Status().Should().Be(StatusCodes.Status403Forbidden);
        result.Detail().Should().Contain("not enabled");
    }

    [Fact]
    public async Task FailRemaining_OnBehalfOfAStreamer_ByTheirDelegatedModerator_Succeeds()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        var moderator = await Fixtures.AddUserAsync(db, "mod");
        var enrolment = await db.EventCompetitors.SingleAsync(ec => ec.EventId == f.Event.Id && ec.UserId == f.Competitor.Id);
        enrolment.IsStreamer = true;
        db.EventCompetitorModerators.Add(new EventCompetitorModerator
        {
            EventId = f.Event.Id,
            CompetitorUserId = f.Competitor.Id,
            ModeratorUserId = moderator.Id,
        });
        await db.SaveChangesAsync();

        var result = await FailRemainingAsync(CreateDbContext(), f, caller: moderator, onBehalfOfUserId: f.Competitor.Id);

        result.Status().Should().Be(StatusCodes.Status200OK);
        var row = await CreateDbContext().FailedObjectives.SingleAsync(x => x.ObjectiveId == f.Objective.Id);
        row.UserId.Should().Be(f.Competitor.Id, "the record belongs to the streamer, not the moderator");
        (await CreateDbContext().AuditLogs.SingleAsync(a => a.Type == AuditEventTypes.ObjectivesRemainingFailed))
            .ActorUserId.Should().Be(moderator.Id);
    }

    [Fact]
    public async Task FailRemaining_WhileATrialRecords_LandsOnTheTrialRecordOnly()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, allowTrialRuns: true);
        await Fixtures.AddObjectiveAsync(db, f.Game, "second");
        var run = await Fixtures.AddTrialRunAsync(db, f);

        var result = await FailRemainingAsync(CreateDbContext(), f, expectedTrialRunId: run.Id);

        result.Status().Should().Be(StatusCodes.Status200OK);
        result.Value<FailRemainingObjectivesResponse>().FailedCount.Should().Be(2);
        var verify = CreateDbContext();
        (await verify.FailedObjectives.Where(x => x.UserId == f.Competitor.Id).ToListAsync())
            .Should().OnlyContain(x => x.TrialRunId == run.Id, "a recording trial owns every write");
        var official = (await ScoreboardEndpoint.BuildAsync(f.Event.Id, verify, default))!
            .Entries.Single(e => e.UserId == f.Competitor.Id);
        official.IsFinished.Should().BeFalse("the official record is untouched");
    }

    [Fact]
    public async Task FailRemaining_WithAStaleTrialAssertion_Conflicts()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, allowTrialRuns: true);

        // The client believed a run was recording; none is, so the write would
        // silently land on the official record. It must refuse instead.
        var result = await FailRemainingAsync(db, f, expectedTrialRunId: Guid.NewGuid());

        result.Status().Should().Be(StatusCodes.Status409Conflict);
        (await CreateDbContext().FailedObjectives.CountAsync()).Should().Be(0);
    }

    private Task<IResult> FailRemainingAsync(
        AppDbContext db,
        EventFixture f,
        User? caller = null,
        Guid? onBehalfOfUserId = null,
        Guid? expectedTrialRunId = null) =>
        FailedObjectivesEndpoint.FailRemainingObjectives(
            f.Event.Id,
            f.Game.Id,
            (caller ?? f.Competitor).Principal(),
            db,
            Audit,
            Cache,
            NullLogger<FailedObjectivesEndpoint>.Instance,
            default,
            onBehalfOfUserId: onBehalfOfUserId,
            expectedTrialRunId: expectedTrialRunId);
}
