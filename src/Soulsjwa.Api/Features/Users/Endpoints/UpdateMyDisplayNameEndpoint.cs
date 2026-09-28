using System.Security.Claims;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Common;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Diagnostics;
using Soulsjwa.Api.Features.Audits.Services;
using Soulsjwa.Api.Features.Events;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.Api.Features.Users.Endpoints;

/// <summary>Null, empty or whitespace clears the override.</summary>
public sealed record UpdateDisplayNameRequest(string? DisplayName);

/// <summary>
/// Lets signed-in users choose the name shown for them, overriding the one
/// Twitch reports; it survives sign-in until they clear it.
/// </summary>
public class UpdateMyDisplayNameEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPatch(ApiRoutes.Prefix + "/users/me/display-name", Handle)
            .WithName("UpdateMyDisplayName")
            .WithSummary("Sets or clears the caller's display name override")
            .Produces<UserResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .RequireAuthorization();
    }

    internal static async Task<IResult> Handle(
        UpdateDisplayNameRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        ILogger<UpdateMyDisplayNameEndpoint> logger,
        CancellationToken ct)
    {
        if (!DisplayNames.TryNormalize(request.DisplayName, out var displayNameOverride, out var error))
            return Results.ValidationProblem(new Dictionary<string, string[]> { [DisplayNames.FieldName] = [error!] });

        var userId = EventOwnership.GetUserId(principal);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return Results.Problem(detail: "User not found.", statusCode: StatusCodes.Status404NotFound);

        if (await DisplayNames.ApplyAsync(db, audit, cache, user, displayNameOverride, userId, ct))
            logger.UserDisplayNameChanged(user.Id, user.DisplayNameOverride is not null, userId);

        return Results.Ok(UserResponse.From(user));
    }
}
