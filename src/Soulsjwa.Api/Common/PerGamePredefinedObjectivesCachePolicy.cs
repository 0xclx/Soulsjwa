using Microsoft.AspNetCore.OutputCaching;

namespace Soulsjwa.Api.Common;

/// <summary>
/// <see cref="PerEventScoreboardCachePolicy"/>'s approach applied to the
/// <c>gameId</c> query parameter instead of a route value.
/// </summary>
public sealed class PerGamePredefinedObjectivesCachePolicy : IOutputCachePolicy
{
    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        if (int.TryParse(context.HttpContext.Request.Query["gameId"], out var gameId))
            context.Tags.Add(CacheTags.PredefinedObjectives(gameId));
        else
            context.Tags.Add(CacheTags.PredefinedObjectivesAll);

        return ValueTask.CompletedTask;
    }

    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;
}
