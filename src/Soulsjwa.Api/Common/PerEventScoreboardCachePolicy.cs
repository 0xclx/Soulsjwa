using Microsoft.AspNetCore.OutputCaching;

namespace Soulsjwa.Api.Common;

/// <summary>
/// Tags a cached response with <see cref="CacheTags.Scoreboard"/> for the request's
/// <c>eventId</c> route value. A per-request policy because a static <c>.Tag(...)</c>
/// at policy-definition time can't see route values.
/// </summary>
public sealed class PerEventScoreboardCachePolicy : IOutputCachePolicy
{
    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        if (context.HttpContext.Request.RouteValues.TryGetValue("eventId", out var raw)
            && Guid.TryParse(raw?.ToString(), out var eventId))
        {
            context.Tags.Add(CacheTags.Scoreboard(eventId));
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;
}
