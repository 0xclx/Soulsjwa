using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Audits.Services;
using Xunit;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// <see cref="AuditService.Log"/> appends a row to the context; the row's
/// foreign keys (actor, subject, event) are real, so this runs on Postgres.
/// </summary>
public class AuditServiceTests : IntegrationTestBase
{
    [Fact]
    public async Task Log_AppendsRowAfterSaveChanges()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        IAuditService audit = new AuditService();

        audit.Log(db, AuditEventTypes.EventCreated, f.Owner.Id,
            eventId: f.Event.Id, subjectUserId: f.Competitor.Id,
            before: null,
            after: new { Name = "Test", IsArchived = false },
            reason: "first event");
        await db.SaveChangesAsync();

        var row = await CreateDbContext().AuditLogs
            .SingleAsync(a => a.EventId == f.Event.Id && a.Type == AuditEventTypes.EventCreated);
        row.ActorUserId.Should().Be(f.Owner.Id);
        row.SubjectUserId.Should().Be(f.Competitor.Id);
        row.AfterJson.Should().NotBeNull().And.Contain("Test");
        row.BeforeJson.Should().BeNull();
        row.Reason.Should().Be("first event");
        row.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Log_SerializesNullablesAsNullJson()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        IAuditService audit = new AuditService();

        audit.Log(db, AuditEventTypes.EventArchived, f.Owner.Id, eventId: f.Event.Id);
        await db.SaveChangesAsync();

        var row = await CreateDbContext().AuditLogs
            .SingleAsync(a => a.EventId == f.Event.Id && a.Type == AuditEventTypes.EventArchived);
        row.BeforeJson.Should().BeNull();
        row.AfterJson.Should().BeNull();
        row.Reason.Should().BeNull();
    }
}
