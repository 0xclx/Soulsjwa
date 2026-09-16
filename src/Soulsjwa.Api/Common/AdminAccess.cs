using Soulsjwa.Api.Features.Events;

namespace Soulsjwa.Api.Common;

/// <summary>
/// Marks an endpoint as admin-only. Attached by <see cref="AdminAccess.RequireAdmin"/>
/// and read by the route-coverage test in <c>Soulsjwa.ApiTests</c>, so a route
/// that should be admin-only but is not marked, or one whose marker is
/// removed, fails a test rather than opening up silently.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class AdminOnlyAttribute : Attribute;

/// <summary>
/// Declarative admin gate for routes. Admin authorization used to be a call
/// each handler had to remember (<c>if (!IsAdmin) return ForbidAdmin()</c>);
/// forgetting it left the route open to any allowlisted user. Applying
/// <see cref="RequireAdmin"/> at the group level makes every route in that
/// group admin-only by default, including ones added later. Handlers keep
/// their explicit check as well: it is what the integration tests, which
/// call handlers directly, exercise.
/// </summary>
public static class AdminAccess
{
    public const string ForbiddenDetail = "This action requires admin privileges.";

    public static IResult Forbid() => Results.Problem(
        detail: ForbiddenDetail,
        statusCode: StatusCodes.Status403Forbidden);

    /// <summary>
    /// Requires an authenticated admin: anonymous callers get the usual 401
    /// from authorization, everyone else who is not an admin the same 403
    /// problem the handlers return, so the response does not depend on which
    /// layer refused.
    /// </summary>
    public static TBuilder RequireAdmin<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
    {
        builder.RequireAuthorization();
        builder.WithMetadata(new AdminOnlyAttribute());
        builder.AddEndpointFilter(async (context, next) =>
            EventOwnership.IsAdmin(context.HttpContext.User) ? await next(context) : Forbid());
        return builder;
    }
}
