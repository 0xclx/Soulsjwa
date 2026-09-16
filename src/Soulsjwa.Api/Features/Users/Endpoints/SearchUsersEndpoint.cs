using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Infrastructure.Data;
using Soulsjwa.Api.Common;

using Soulsjwa.Api.Features.Users;

namespace Soulsjwa.Api.Features.Users.Endpoints;

public sealed record UserSearchResultResponse(
    Guid Id,
    string TwitchLogin,
    string DisplayName,
    string? ProfileImageUrl,
    bool IsPending);

/// <summary>
/// Lightweight user lookup for UI pickers (e.g. "add competitor" / "add
/// moderator" forms). Returns at most a small number of matches and is
/// restricted to authenticated callers so anonymous scraping isn't possible.
///
/// Handlers are <c>internal</c> rather than <c>private</c> so
/// <c>Soulsjwa.IntegrationTests</c> can invoke them directly — see
/// <see cref="Soulsjwa.Api.Features.Events.Endpoints.CompletedObjectivesEndpoint"/>
/// for why.
/// </summary>
public class SearchUsersEndpoint : IEndpoint
{
    private const int MaxResults = 20;
    private const int MinQueryLength = 2;

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.Prefix + "/users/search", Handle)
            .WithName("SearchUsers")
            .WithSummary("Searches for users by Twitch login or display name")
            .Produces<List<UserSearchResultResponse>>(StatusCodes.Status200OK)
            .RequireAuthorization();
    }

    internal static async Task<IResult> Handle(
        AppDbContext db, CancellationToken ct, string? q = null, int limit = MaxResults)
    {
        var query = (q ?? string.Empty).Trim();
        if (query.Length < MinQueryLength)
            return Results.Ok(new List<UserSearchResultResponse>());

        limit = Math.Clamp(limit, 1, MaxResults);
        var lower = query.ToLower();

        // Placeholder users created via "add competitor by Twitch handle" carry
        // a sentinel TwitchId of "pending:<login>"; we surface that with the
        // IsPending flag so the UI can hint that the user hasn't logged in yet.
        //
        // Contains() is a leading-wildcard search, which IX_Users_TwitchLogin_Lower
        // can't serve — a functional B-tree index only helps equality and prefix
        // matches. Accepted as a sequential scan rather than adding a trigram
        // index: this endpoint is authenticated, capped at MaxResults, and
        // requires MinQueryLength+ characters, so the scan stays bounded and rare.
        var results = await db.Users
            .Where(u => u.TwitchLogin.ToLower().Contains(lower)
                     || u.DisplayName.ToLower().Contains(lower))
            .OrderBy(u => u.TwitchLogin)
            .Take(limit)
            .Select(u => new UserSearchResultResponse(
                u.Id, u.TwitchLogin, u.DisplayName, u.ProfileImageUrl,
                u.TwitchId.StartsWith(PendingUserMarker.Prefix)))
            .ToListAsync(ct);

        return Results.Ok(results);
    }
}
