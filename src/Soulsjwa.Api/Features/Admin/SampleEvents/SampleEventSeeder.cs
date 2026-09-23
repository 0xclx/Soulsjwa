using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Audits.Services;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.Api.Features.Admin.SampleEvents;

/// <summary>
/// Turns <see cref="SampleEventCatalog"/> into rows: the events, their custom
/// games and objectives, any rules text and one <c>event.created</c> audit each,
/// all in a single save — so a failure leaves nothing behind. Called by the
/// admin endpoint and directly by IntegrationTests, so both get the same events.
/// </summary>
internal static class SampleEventSeeder
{
    public sealed record CreatedEvent(Guid Id, string Name);

    public static async Task<IReadOnlyList<CreatedEvent>> CreateAsync(
        AppDbContext db,
        IAuditService audit,
        Guid ownerId,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var created = new List<CreatedEvent>();
        foreach (var definition in SampleEventCatalog.Build())
        {
            var ev = new Event
            {
                Name = definition.Name,
                Description = definition.Description,
                CreatedById = ownerId,
                TieBreakMode = definition.TieBreakMode,
                IsStarted = definition.StartedAgo is not null,
                StartedAt = nowUtc - definition.StartedAgo,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc,
            };
            db.Events.Add(ev);

            for (var gameIndex = 0; gameIndex < definition.Games.Count; gameIndex++)
            {
                var gameDefinition = definition.Games[gameIndex];
                var game = new EventGame
                {
                    EventId = ev.Id,
                    CustomGameName = gameDefinition.Name,
                    CustomGameDescription = gameDefinition.Description,
                    IsEnabled = gameIndex == definition.EnabledGameIndex,
                    SortOrder = gameIndex,
                };
                db.EventGames.Add(game);
                db.Objectives.AddRange(gameDefinition.Objectives.Select((objective, objectiveIndex) => new Objective
                {
                    EventGameId = game.Id,
                    Name = objective.Name,
                    Score = objective.Score,
                    Category = objective.Category,
                    SortOrder = objectiveIndex,
                }));
            }

            if (definition.RulesMarkdown is not null)
                db.EventRules.Add(new EventRules { EventId = ev.Id, Content = definition.RulesMarkdown, UpdatedAt = nowUtc });

            audit.Log(db, AuditEventTypes.EventCreated, ownerId, eventId: ev.Id,
                after: new { ev.Name, Sample = true });
            created.Add(new CreatedEvent(ev.Id, ev.Name));
        }

        await db.SaveChangesAsync(ct);
        return created;
    }
}
