using System.Security.Claims;
using Soulsjwa.Api.Common;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Diagnostics;
using Soulsjwa.Api.Features.Admin.SampleEvents;
using Soulsjwa.Api.Features.Audits.Services;
using Soulsjwa.Api.Features.Events;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.Api.Features.Admin.Endpoints;

public sealed record SampleEventResponse(Guid Id, string Name);

public sealed record CreateSampleEventsResponse(IReadOnlyList<SampleEventResponse> Events);

/// <summary>
/// Creates the fixed set of "Sample: " events (see <see cref="SampleEventCatalog"/>)
/// owned by the calling admin, for testing the UI in any environment. Every
/// call creates a fresh set; archiving hides one.
/// </summary>
public class AdminSampleEventsEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGroup(ApiRoutes.Prefix + "/admin/sample-events").RequireAdmin()
            .MapPost("/", Create)
            .WithName("AdminCreateSampleEvents")
            .WithSummary("Creates the fixed set of sample events, owned by the caller (admin only)")
            .Produces<CreateSampleEventsResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization();
    }

    internal static async Task<IResult> Create(
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        ILogger<AdminSampleEventsEndpoint> logger,
        CancellationToken ct)
    {
        if (!EventOwnership.IsAdmin(principal)) return AdminAccess.Forbid();

        var userId = EventOwnership.GetUserId(principal);
        var created = await SampleEventSeeder.CreateAsync(db, audit, userId, DateTime.UtcNow, ct);
        logger.AdminSampleEventsCreated(created.Count, userId);

        return Results.Created(
            ApiRoutes.Prefix + "/events",
            new CreateSampleEventsResponse(created.Select(e => new SampleEventResponse(e.Id, e.Name)).ToList()));
    }
}
