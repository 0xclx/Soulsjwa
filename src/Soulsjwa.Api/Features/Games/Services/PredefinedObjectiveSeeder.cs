using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Diagnostics;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Features.Games.Definitions;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.Api.Features.Games.Services;

/// <summary>
/// Seeds predefined objectives for supported games on application startup, adding
/// new ones from the code definitions without duplicating existing rows.
/// </summary>
public static class PredefinedObjectiveSeeder
{
    public static async Task SeedAsync(
        AppDbContext db,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        using var operation = DiagnosticsConfig.StartBusinessOperation(
            DiagnosticsConfig.BusinessOperationNames.SeedPredefinedObjectives,
            DiagnosticsConfig.ActivityNames.SeedPredefinedObjectives);
        var definitions = PredefinedObjectives.GetAll();
        var added = 0;

        // One query for the whole catalog, then an in-memory diff. This used
        // to be one FirstOrDefault per definition — about a thousand round
        // trips on every start of the api container and of the migrate job.
        //
        // Identity is (game, rule), not the display name: the rule pins down
        // which underlying flag the objective tracks. Name is NOT safe to
        // match on — the same boss can appear in several locations under an
        // identical name (e.g. "Black Knife Assassin - Slain" in both Bellum
        // Highway and Stormhill) while tracking different flags, and matching
        // by name would collapse those into one row and drop a location's
        // tracking. Rules are compared in canonical form because jsonb
        // normalises what it stores (see RuleJson.Canonicalize).
        var existingByIdentity = new Dictionary<(int GameId, string Rule), Objective>();
        var existingRows = await db.Objectives
            .Where(o => o.IsPredefined && o.EventGameId == null && o.GameId != null && o.Rule != null)
            .ToListAsync(ct);
        foreach (var row in existingRows)
            existingByIdentity.TryAdd((row.GameId!.Value, RuleJson.Canonicalize(row.Rule!)), row);

        foreach (var def in definitions)
        {
            var identity = (def.GameId, RuleJson.Canonicalize(def.Rule));
            if (existingByIdentity.TryGetValue(identity, out var existing))
            {
                // Backfill fields that can change across a code deploy without
                // the underlying rule changing (e.g. a naming/category cleanup)
                // so existing installs pick these up without a manual re-seed.
                if (existing.Name != def.Name)
                    existing.Name = def.Name;
                if (existing.Category != def.Category)
                    existing.Category = def.Category;
                if (!SameRule(existing.FailRule, def.FailRule))
                    existing.FailRule = def.FailRule;
                continue;
            }

            var objective = new Objective
            {
                GameId = def.GameId,
                Name = def.Name,
                Score = def.Score,
                Category = def.Category,
                Rule = def.Rule,
                FailRule = def.FailRule,
                IsPredefined = true,
                EventGameId = null
            };
            db.Objectives.Add(objective);
            // Guards against a definition list that repeats a (game, rule),
            // which would otherwise insert twice on one run.
            existingByIdentity[identity] = objective;
            added++;
        }

        await db.SaveChangesAsync(ct);
        operation.SetTags(
            (DiagnosticsConfig.Tags.ObjectivesDefined, definitions.Count),
            (DiagnosticsConfig.Tags.ObjectivesAdded, added));
        logger?.PredefinedObjectivesSeeded(added, definitions.Count);
    }

    private static bool SameRule(string? stored, string? defined) =>
        stored is null || defined is null
            ? stored == defined
            : RuleJson.Canonicalize(stored) == RuleJson.Canonicalize(defined);
}
