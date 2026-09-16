using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Common;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Audits.Services;
using Soulsjwa.Api.Features.Calendar.Entities;
using Soulsjwa.Api.Features.Events;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.Api.Features.Calendar.Endpoints;

public sealed record CalendarEntryResponse(
    Guid Id,
    Guid EventId,
    string Title,
    string? DescriptionMarkdown,
    DateTime StartsAt,
    DateTime EndsAt,
    bool IsAllDay,
    bool IsHighlighted,
    string Color,
    Guid? ImageAssetId,
    string? ImageUrl,
    Guid CreatedById,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    uint Version);

public sealed record CreateCalendarEntryRequest(
    string Title,
    string? DescriptionMarkdown,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    bool IsAllDay,
    bool IsHighlighted,
    string Color,
    Guid? ImageAssetId);

public sealed record UpdateCalendarEntryRequest(
    string Title,
    string? DescriptionMarkdown,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    bool IsAllDay,
    bool IsHighlighted,
    string Color,
    Guid? ImageAssetId,
    uint? Version = null);

/// <summary>
/// CRUD for an event's admin-authored calendar entries. Readable
/// by everyone; written by the event owner or an admin (<see cref="EventOwnership.RequireOwner"/>).
///
/// Handlers are <c>internal</c> rather than <c>private</c> so
/// <c>Soulsjwa.IntegrationTests</c> can invoke them directly — see
/// <see cref="Soulsjwa.Api.Features.Events.Endpoints.CompletedObjectivesEndpoint"/>
/// for why.
/// </summary>
public class CalendarEntriesEndpoint : IEndpoint
{
    public const int MaxTitleLength = 120;
    public const int MaxDescriptionBytes = 64 * 1024;

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ApiRoutes.Prefix + "/events/{eventId:guid}/calendar-entries");

        group.MapGet("/", List)
            .WithName("ListCalendarEntries")
            .WithSummary("Lists an event's calendar entries")
            .Produces<List<CalendarEntryResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AllowAnonymous();

        group.MapPost("/", Create)
            .WithName("CreateCalendarEntry")
            .WithSummary("Creates a calendar entry (owner/admin only)")
            .Produces<CalendarEntryResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .RequireAuthorization();

        group.MapPut("/{entryId:guid}", Update)
            .WithName("UpdateCalendarEntry")
            .WithSummary("Updates a calendar entry (owner/admin only)")
            .Produces<CalendarEntryResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization();

        group.MapDelete("/{entryId:guid}", Delete)
            .WithName("DeleteCalendarEntry")
            .WithSummary("Deletes a calendar entry (owner/admin only)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }

    internal static async Task<IResult> List(Guid eventId, AppDbContext db, CancellationToken ct)
    {
        var eventExists = await db.Events.AnyAsync(e => e.Id == eventId, ct);
        if (!eventExists)
            return Results.Problem(detail: "Event not found.", statusCode: StatusCodes.Status404NotFound);

        var entries = await db.CalendarEntries
            .Where(e => e.EventId == eventId)
            .OrderBy(e => e.StartsAt)
            .ToListAsync(ct);

        return Results.Ok(entries.Select(e => ToResponse(db, e)).ToList());
    }

    internal static async Task<IResult> Create(
        Guid eventId,
        CreateCalendarEntryRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        CancellationToken ct)
    {
        var (_, error) = await EventContext.RequireOwnedEventAsync(eventId, principal, "create calendar entries", db, ct);
        if (error is not null) return error;

        var (color, errors) = await ValidateAsync(request.Title, request.DescriptionMarkdown, request.StartsAt,
            request.EndsAt, request.Color, request.ImageAssetId, db, ct);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var now = DateTime.UtcNow;
        var entry = new CalendarEntry
        {
            EventId = eventId,
            Title = request.Title.Trim(),
            DescriptionMarkdown = NormalizeDescription(request.DescriptionMarkdown),
            StartsAt = UtcTime.ToStorage(request.StartsAt),
            EndsAt = UtcTime.ToStorage(request.EndsAt),
            IsAllDay = request.IsAllDay,
            IsHighlighted = request.IsHighlighted,
            Color = color,
            ImageAssetId = request.ImageAssetId,
            CreatedById = EventOwnership.GetUserId(principal),
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.CalendarEntries.Add(entry);
        var callerId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.CalendarEntryCreated, callerId, eventId: eventId,
            after: new { entry.Id, entry.Title, entry.StartsAt, entry.EndsAt, entry.IsAllDay, entry.IsHighlighted, Color = entry.Color.ToString() });
        await db.SaveChangesAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Calendar, ct);

        return Results.Created(
            $"/api/v1/events/{eventId}/calendar-entries/{entry.Id}",
            ToResponse(db, entry));
    }

    internal static async Task<IResult> Update(
        Guid eventId,
        Guid entryId,
        UpdateCalendarEntryRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        CancellationToken ct)
    {
        var (ev, error) = await EventContext.RequireEventAsync(eventId, db, ct);
        if (error is not null) return error;

        var entry = await db.CalendarEntries.FirstOrDefaultAsync(e => e.Id == entryId && e.EventId == eventId, ct);
        if (entry is null)
            return Results.Problem(detail: "Calendar entry not found.", statusCode: StatusCodes.Status404NotFound);

        if (EventOwnership.RequireOwner(ev!, principal, "update calendar entries") is { } ownerError)
            return ownerError;

        var (color, errors) = await ValidateAsync(request.Title, request.DescriptionMarkdown, request.StartsAt,
            request.EndsAt, request.Color, request.ImageAssetId, db, ct);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        ConcurrencyToken.ApplyExpectedVersion(db, entry, request.Version);

        var before = new { entry.Title, entry.StartsAt, entry.EndsAt, entry.IsAllDay, entry.IsHighlighted, Color = entry.Color.ToString() };

        entry.Title = request.Title.Trim();
        entry.DescriptionMarkdown = NormalizeDescription(request.DescriptionMarkdown);
        entry.StartsAt = UtcTime.ToStorage(request.StartsAt);
        entry.EndsAt = UtcTime.ToStorage(request.EndsAt);
        entry.IsAllDay = request.IsAllDay;
        entry.IsHighlighted = request.IsHighlighted;
        entry.Color = color;
        entry.ImageAssetId = request.ImageAssetId;
        entry.UpdatedAt = DateTime.UtcNow;

        var callerId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.CalendarEntryUpdated, callerId, eventId: eventId,
            before: before,
            after: new { entry.Title, entry.StartsAt, entry.EndsAt, entry.IsAllDay, entry.IsHighlighted, Color = entry.Color.ToString() });

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

        await cache.EvictByTagAsync(CacheTags.Calendar, ct);

        return Results.Ok(ToResponse(db, entry));
    }

    internal static async Task<IResult> Delete(
        Guid eventId,
        Guid entryId,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        CancellationToken ct)
    {
        var (ev, error) = await EventContext.RequireEventAsync(eventId, db, ct);
        if (error is not null) return error;

        var entry = await db.CalendarEntries.FirstOrDefaultAsync(e => e.Id == entryId && e.EventId == eventId, ct);
        if (entry is null)
            return Results.Problem(detail: "Calendar entry not found.", statusCode: StatusCodes.Status404NotFound);

        if (EventOwnership.RequireOwner(ev!, principal, "delete calendar entries") is { } ownerError)
            return ownerError;

        db.CalendarEntries.Remove(entry);
        var callerId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.CalendarEntryDeleted, callerId, eventId: eventId,
            before: new { entry.Id, entry.Title, entry.StartsAt, entry.EndsAt });
        await db.SaveChangesAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Calendar, ct);

        return Results.NoContent();
    }

    private static async Task<(CalendarEntryColor Color, Dictionary<string, string[]> Errors)> ValidateAsync(
        string? title,
        string? descriptionMarkdown,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        string? colorRaw,
        Guid? imageAssetId,
        AppDbContext db,
        CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(title))
            errors["title"] = ["Title is required."];
        else if (title.Length > MaxTitleLength)
            errors["title"] = [$"Title must be {MaxTitleLength} characters or fewer."];

        if (descriptionMarkdown is not null &&
            Encoding.UTF8.GetByteCount(descriptionMarkdown) > MaxDescriptionBytes)
        {
            errors["descriptionMarkdown"] = [$"DescriptionMarkdown must be {MaxDescriptionBytes:N0} bytes or fewer."];
        }

        if (endsAt <= startsAt)
            errors["endsAt"] = ["EndsAt must be after StartsAt."];

        if (!Enum.TryParse(colorRaw, ignoreCase: false, out CalendarEntryColor color) || !Enum.IsDefined(color))
            errors["color"] = [$"Color must be one of: {string.Join(", ", Enum.GetNames<CalendarEntryColor>())}."];

        if (imageAssetId is { } assetId && !await db.MediaAssets.AnyAsync(a => a.Id == assetId, ct))
            errors["imageAssetId"] = ["ImageAssetId does not reference an existing media asset."];

        return (color, errors);
    }

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description;

    private static CalendarEntryResponse ToResponse(AppDbContext db, CalendarEntry e) => new(
        e.Id,
        e.EventId,
        e.Title,
        e.DescriptionMarkdown,
        e.StartsAt,
        e.EndsAt,
        e.IsAllDay,
        e.IsHighlighted,
        e.Color.ToString(),
        e.ImageAssetId,
        e.ImageAssetId is { } assetId ? $"/api/v1/media/{assetId}" : null,
        e.CreatedById,
        e.CreatedAt,
        e.UpdatedAt,
        ConcurrencyToken.GetXmin(db, e));
}
