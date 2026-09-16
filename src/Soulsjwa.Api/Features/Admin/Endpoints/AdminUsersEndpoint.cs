using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Common.Models;
using Soulsjwa.Api.Diagnostics;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Audits.Services;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events;
using Soulsjwa.Api.Infrastructure.Data;
using Soulsjwa.Api.Common;

namespace Soulsjwa.Api.Features.Admin.Endpoints;

public sealed record AdminUserResponse(
    Guid Id,
    string TwitchLogin,
    string DisplayName,
    string Role,
    bool IsAllowlisted,
    DateTime CreatedAt);

public sealed record SetRoleRequest(string Role);

/// <summary>
/// Admin endpoints for inspecting users and changing their role.
///
/// Handlers are <c>internal</c> rather than <c>private</c> so
/// <c>Soulsjwa.IntegrationTests</c> can invoke them directly — see
/// <see cref="Soulsjwa.Api.Features.Events.Endpoints.CompletedObjectivesEndpoint"/>
/// for why.
/// </summary>
public class AdminUsersEndpoint : IEndpoint
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ApiRoutes.Prefix + "/admin/users").RequireAdmin();

        group.MapGet("/", List)
            .WithName("AdminListUsers")
            .WithSummary("Lists users with pagination (admin only)")
            .Produces<PaginatedResponse<AdminUserResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization();

        group.MapPatch("/{id:guid}/role", SetRole)
            .WithName("AdminSetUserRole")
            .WithSummary("Promotes or demotes a user (admin only)")
            .Produces<AdminUserResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization();
    }

    internal static async Task<IResult> List(
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken ct,
        int page = 1,
        int pageSize = DefaultPageSize,
        string? search = null)
    {
        if (!EventOwnership.IsAdmin(principal)) return AdminAccess.Forbid();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = db.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(u => u.TwitchLogin.ToLower().Contains(s) || u.DisplayName.ToLower().Contains(s));
        }

        var total = await query.CountAsync(ct);
        var users = await query
            .OrderBy(u => u.TwitchLogin)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AdminUserResponse(
                u.Id, u.TwitchLogin, u.DisplayName, u.Role.ToString(), u.IsAllowlisted, u.CreatedAt))
            .ToListAsync(ct);

        return Results.Ok(new PaginatedResponse<AdminUserResponse>(users, total, page, pageSize));
    }

    internal static async Task<IResult> SetRole(
        Guid id,
        SetRoleRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        ILogger<AdminUsersEndpoint> logger,
        CancellationToken ct)
    {
        if (!EventOwnership.IsAdmin(principal)) return AdminAccess.Forbid();

        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: false, out var role))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Role"] = [$"Role must be one of: {string.Join(", ", Enum.GetNames<UserRole>())}."]
            });

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
            return Results.Problem(detail: "User not found.", statusCode: StatusCodes.Status404NotFound);

        // Prevent removing the last admin so the system can always be managed.
        if (user.Role == UserRole.Admin && role != UserRole.Admin)
        {
            var adminCount = await db.Users.CountAsync(u => u.Role == UserRole.Admin, ct);
            if (adminCount <= 1)
                return Results.Problem(
                    detail: "Cannot demote the last remaining admin.",
                    statusCode: StatusCodes.Status409Conflict);
        }

        var beforeRole = user.Role.ToString();
        user.Role = role;
        user.UpdatedAt = DateTime.UtcNow;
        var actorId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.UserRoleChanged, actorId,
            subjectUserId: user.Id,
            before: new { Role = beforeRole },
            after: new { Role = user.Role.ToString() });
        await db.SaveChangesAsync(ct);
        logger.AdminUserRoleSet(user.Id, user.Role.ToString(), actorId);

        return Results.Ok(new AdminUserResponse(
            user.Id, user.TwitchLogin, user.DisplayName, user.Role.ToString(), user.IsAllowlisted, user.CreatedAt));
    }
}
