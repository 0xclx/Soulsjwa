using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Diagnostics;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Audits.Services;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events;
using Soulsjwa.Api.Infrastructure.Data;
using Soulsjwa.Api.Common;

namespace Soulsjwa.Api.Features.Admin.Endpoints;

public sealed record AllowlistEntryResponse(
    Guid Id,
    string TwitchLogin,
    string? Note,
    DateTime CreatedAt,
    Guid? AddedById,
    Guid? LinkedUserId,
    string? LinkedDisplayName);

public sealed record AddAllowlistRequest(string TwitchLogin, string? Note);

/// <summary>
/// Admin-managed Twitch login allowlist. Only users whose Twitch login appears
/// here may complete the Twitch OAuth flow and create / use an account.
///
/// Handlers are <c>internal</c> rather than <c>private</c> so
/// <c>Soulsjwa.IntegrationTests</c> can invoke them directly — see
/// <see cref="Soulsjwa.Api.Features.Events.Endpoints.CompletedObjectivesEndpoint"/>
/// for why.
/// </summary>
public class AllowlistEndpoint : IEndpoint
{
    private const int MaxLoginLength = 64;
    private const int MaxNoteLength = 500;

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ApiRoutes.Prefix + "/admin/allowlist").RequireAdmin();

        group.MapGet("/", List)
            .WithName("ListAllowlist")
            .WithSummary("Lists all allowlisted Twitch logins (admin only)")
            .Produces<List<AllowlistEntryResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization();

        group.MapPost("/", Add)
            .WithName("AddAllowlistEntry")
            .WithSummary("Adds a Twitch login to the allowlist (admin only)")
            .Produces<AllowlistEntryResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization();

        group.MapDelete("/{id:guid}", Remove)
            .WithName("RemoveAllowlistEntry")
            .WithSummary("Removes a Twitch login from the allowlist and revokes the matching user's access (admin only)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }

    internal static async Task<IResult> List(
        ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        if (!EventOwnership.IsAdmin(principal)) return AdminAccess.Forbid();

        var entries = await db.AllowlistedTwitchLogins
            .OrderBy(a => a.TwitchLogin)
            .ToListAsync(ct);

        var logins = entries.Select(e => e.TwitchLogin).ToList();
        var users = await db.Users
            .Where(u => logins.Contains(u.TwitchLogin.ToLower()))
            .Select(u => new { u.Id, u.TwitchLogin, u.DisplayName })
            .ToListAsync(ct);

        var response = entries.Select(e =>
        {
            var u = users.FirstOrDefault(u => u.TwitchLogin.Equals(e.TwitchLogin, StringComparison.OrdinalIgnoreCase));
            return new AllowlistEntryResponse(
                e.Id, e.TwitchLogin, e.Note, e.CreatedAt, e.AddedById,
                u?.Id, u?.DisplayName);
        }).ToList();

        return Results.Ok(response);
    }

    internal static async Task<IResult> Add(
        AddAllowlistRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        ILogger<AllowlistEndpoint> logger,
        CancellationToken ct)
    {
        if (!EventOwnership.IsAdmin(principal)) return AdminAccess.Forbid();

        var login = request.TwitchLogin?.Trim().ToLowerInvariant() ?? string.Empty;
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrEmpty(login))
            errors["TwitchLogin"] = ["TwitchLogin is required."];
        else if (login.Length > MaxLoginLength)
            errors["TwitchLogin"] = [$"TwitchLogin must be {MaxLoginLength} characters or fewer."];
        if (request.Note is { Length: > MaxNoteLength })
            errors["Note"] = [$"Note must be {MaxNoteLength} characters or fewer."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var existing = await db.AllowlistedTwitchLogins
            .AnyAsync(a => a.TwitchLogin == login, ct);
        if (existing)
            return Results.Problem(
                detail: "This Twitch login is already on the allowlist.",
                statusCode: StatusCodes.Status409Conflict);

        var entry = new AllowlistedTwitchLogin
        {
            TwitchLogin = login,
            Note = request.Note?.Trim(),
            AddedById = EventOwnership.GetUserId(principal),
        };
        db.AllowlistedTwitchLogins.Add(entry);

        // If the user already exists (was de-allowlisted previously), re-allow them.
        var existingUser = await db.Users.FirstOrDefaultAsync(u => u.TwitchLogin.ToLower() == login, ct);
        if (existingUser is not null)
            existingUser.IsAllowlisted = true;

        var actorId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.AllowlistAdded, actorId,
            subjectUserId: existingUser?.Id,
            after: new { entry.TwitchLogin, entry.Note });
        await db.SaveChangesAsync(ct);
        logger.AllowlistLoginAdded(entry.TwitchLogin, actorId, existingUser?.Id);

        return Results.Created($"/api/v1/admin/allowlist/{entry.Id}",
            new AllowlistEntryResponse(entry.Id, entry.TwitchLogin, entry.Note, entry.CreatedAt,
                entry.AddedById, existingUser?.Id, existingUser?.DisplayName));
    }

    internal static async Task<IResult> Remove(
        Guid id,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        ILogger<AllowlistEndpoint> logger,
        CancellationToken ct)
    {
        if (!EventOwnership.IsAdmin(principal)) return AdminAccess.Forbid();

        var entry = await db.AllowlistedTwitchLogins.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (entry is null)
            return Results.Problem(detail: "Allowlist entry not found.", statusCode: StatusCodes.Status404NotFound);

        // Revoke access for the linked user (if any) so future logins are rejected
        // and any active refresh token chain can't outlive the de-allowlisting.
        var linked = await db.Users
            .FirstOrDefaultAsync(u => u.TwitchLogin.ToLower() == entry.TwitchLogin, ct);
        var revokedApiKeyCount = 0;
        if (linked is not null)
        {
            linked.IsAllowlisted = false;
            var tokens = await db.RefreshTokens
                .Where(t => t.UserId == linked.Id && !t.IsRevoked)
                .ToListAsync(ct);
            foreach (var t in tokens)
            {
                t.IsRevoked = true;
                t.RevokedAt = DateTime.UtcNow;
            }

            // Revoke API keys too — they otherwise survive de-allowlisting entirely,
            // since ApiKeyAuthHandler is the only place IsAllowlisted is re-checked.
            var apiKeys = await db.ApiKeys
                .Where(k => k.UserId == linked.Id && !k.IsRevoked)
                .ToListAsync(ct);
            foreach (var k in apiKeys)
                k.IsRevoked = true;
            revokedApiKeyCount = apiKeys.Count;
        }

        db.AllowlistedTwitchLogins.Remove(entry);
        var removeActorId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.AllowlistRemoved, removeActorId,
            subjectUserId: linked?.Id,
            before: new { entry.TwitchLogin, entry.Note, RevokedApiKeyCount = revokedApiKeyCount });
        await db.SaveChangesAsync(ct);
        logger.AllowlistLoginRemoved(entry.TwitchLogin, removeActorId, linked?.Id);
        return Results.NoContent();
    }

}
