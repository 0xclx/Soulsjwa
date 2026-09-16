using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Common;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Audits.Services;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.Api.Features.Events.Endpoints;

public sealed record EventRulesResponse(string? Content, DateTime? UpdatedAt);
public sealed record UpdateEventRulesRequest(string? Content);

/// <summary>
/// An event's rules document: one markdown blob per event, readable by
/// everyone and written by the owner or an admin.
///
/// Handlers are <c>internal</c> rather than <c>private</c> so
/// <c>Soulsjwa.IntegrationTests</c> can invoke them directly — see
/// <see cref="Soulsjwa.Api.Features.Events.Endpoints.CompletedObjectivesEndpoint"/>
/// for why.
/// </summary>
public class EventRulesEndpoint : IEndpoint
{
    /// <summary>Matches the 64 KiB per-document limit shared by every Markdown surface.</summary>
    internal const int MaxContentBytes = 64 * 1024;

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ApiRoutes.Prefix + "/events/{eventId:guid}/rules");

        group.MapGet("/", GetRules)
            .WithName("GetEventRules")
            .WithSummary("Gets an event's rules document")
            .Produces<EventRulesResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AllowAnonymous();

        group.MapPut("/", UpdateRules)
            .WithName("UpdateEventRules")
            .WithSummary("Sets an event's rules document (owner/admin only)")
            .Produces<EventRulesResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization();
    }

    internal static async Task<IResult> GetRules(Guid eventId, AppDbContext db, HttpContext context, CancellationToken ct)
    {
        var eventExists = await db.Events.AnyAsync(e => e.Id == eventId, ct);
        if (!eventExists)
            return Results.Problem(detail: "Event not found.", statusCode: StatusCodes.Status404NotFound);

        var rules = await db.EventRules
            .Where(r => r.EventId == eventId)
            .Select(r => new { r.Content, r.UpdatedAt, Xmin = EF.Property<uint>(r, "xmin") })
            .FirstOrDefaultAsync(ct);

        if (rules is not null)
            context.Response.Headers.ETag = ConcurrencyToken.ToETag(rules.Xmin);

        return Results.Ok(new EventRulesResponse(rules?.Content, rules?.UpdatedAt));
    }

    internal static async Task<IResult> UpdateRules(
        Guid eventId,
        UpdateEventRulesRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        HttpContext context,
        CancellationToken ct)
    {
        var (_, error) = await EventContext.RequireOwnedEventAsync(eventId, principal, "update this event's rules", db, ct);
        if (error is not null) return error;

        var byteCount = request.Content is null ? 0 : Encoding.UTF8.GetByteCount(request.Content);
        if (byteCount > MaxContentBytes)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Content"] = [$"Content must be {MaxContentBytes:N0} bytes or fewer."],
            });
        }

        var rules = await db.EventRules.FirstOrDefaultAsync(r => r.EventId == eventId, ct);
        var beforeContent = rules?.Content;
        if (rules is null)
        {
            rules = new EventRules { EventId = eventId };
            db.EventRules.Add(rules);
        }
        else
        {
            // A new document has no prior token to match against — If-Match
            // only makes sense (and is only applied) against an existing row.
            ConcurrencyToken.ApplyIfMatch(db, rules, context.Request.Headers.IfMatch);
        }

        rules.Content = request.Content;
        rules.UpdatedAt = DateTime.UtcNow;

        var callerId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.EventRulesUpdated, callerId, eventId: eventId,
            before: new { Content = AuditContentDigest.Of(beforeContent) },
            after: new { Content = AuditContentDigest.Of(rules.Content) });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Problem(
                detail: "This record was modified by someone else. Reload and reapply your change.",
                statusCode: StatusCodes.Status409Conflict);
        }

        context.Response.Headers.ETag = ConcurrencyToken.ToETag(ConcurrencyToken.GetXmin(db, rules));
        return Results.Ok(new EventRulesResponse(rules.Content, rules.UpdatedAt));
    }
}
