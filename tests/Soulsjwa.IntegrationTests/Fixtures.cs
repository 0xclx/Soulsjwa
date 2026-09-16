using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// A running event with one enabled game, one objective, an owner and a
/// competitor — the arrangement almost every objective-write test needs.
/// </summary>
public sealed record EventFixture(
    User Owner,
    User Competitor,
    Event Event,
    EventGame Game,
    Objective Objective);

/// <summary>
/// Seeding helpers. Direct entity inserts on purpose: arranging the same state
/// through the endpoints would couple every test to endpoints it is not testing.
/// </summary>
public static class Fixtures
{
    public static async Task<User> AddUserAsync(
        AppDbContext db, string prefix = "user", UserRole role = UserRole.User)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = new User
        {
            TwitchId = $"twitch_{suffix}",
            TwitchLogin = $"{prefix}_{suffix}",
            DisplayName = $"{prefix}_{suffix}",
            Role = role,
            IsAllowlisted = true,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Seeds owner, competitor, event, game and objective in one go.
    /// <paramref name="started"/> and <paramref name="enabled"/> are dials
    /// because they are the two preconditions the objective-write handlers check.
    /// </summary>
    public static async Task<EventFixture> AddEventAsync(
        AppDbContext db,
        int score = 10,
        bool started = true,
        bool enabled = true,
        bool withCompetitor = true,
        int? knownGameId = null,
        bool allowTrialRuns = false)
    {
        var owner = await AddUserAsync(db, "owner");
        var competitor = await AddUserAsync(db, "comp");

        var ev = new Event
        {
            Name = "test event",
            CreatedById = owner.Id,
            IsStarted = started,
            AllowTrialRuns = allowTrialRuns,
        };
        var game = knownGameId is { } id
            ? new EventGame { KnownGameId = id, IsEnabled = enabled }
            : new EventGame { CustomGameName = "Custom Game", IsEnabled = enabled };
        ev.EventGames.Add(game);
        db.Events.Add(ev);
        if (withCompetitor)
            db.EventCompetitors.Add(new EventCompetitor { EventId = ev.Id, UserId = competitor.Id });
        await db.SaveChangesAsync();

        var objective = new Objective { EventGameId = game.Id, Name = "obj", Score = score };
        db.Objectives.Add(objective);
        await db.SaveChangesAsync();

        return new EventFixture(owner, competitor, ev, game, objective);
    }

    public static async Task<Objective> AddObjectiveAsync(
        AppDbContext db, EventGame game, string name = "obj", int score = 10)
    {
        var objective = new Objective { EventGameId = game.Id, Name = name, Score = score };
        db.Objectives.Add(objective);
        await db.SaveChangesAsync();
        return objective;
    }

    public static async Task<TrialRun> AddTrialRunAsync(
        AppDbContext db, EventFixture fixture, TrialRunState state = TrialRunState.Running)
    {
        var run = new TrialRun
        {
            EventId = fixture.Event.Id,
            EventGameId = fixture.Game.Id,
            UserId = fixture.Competitor.Id,
            State = state,
            StartedAt = state == TrialRunState.NotStarted ? null : DateTime.UtcNow,
        };
        db.TrialRuns.Add(run);
        await db.SaveChangesAsync();
        return run;
    }
}
