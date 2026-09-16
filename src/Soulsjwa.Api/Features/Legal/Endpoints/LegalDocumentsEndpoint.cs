using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Common;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Features.Admin.Endpoints;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Audits.Services;
using Soulsjwa.Api.Features.Events;
using Soulsjwa.Api.Features.Legal.Entities;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.Api.Features.Legal.Endpoints;

public sealed record LegalDocumentResponse(string? Content, DateTime? UpdatedAt);
public sealed record UpdateLegalDocumentRequest(string? Content);

public class LegalDocumentsEndpoint : IEndpoint
{
    /// <summary>Shared 64 KiB per-document limit.</summary>
    internal const int MaxContentBytes = 64 * 1024;

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ApiRoutes.Prefix + "/legal/{kind}");

        group.MapGet("/", GetDocument)
            .WithName("GetLegalDocument")
            .WithSummary("Gets a legal document (Impressum or Datenschutz)")
            .Produces<LegalDocumentResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .AllowAnonymous();

        group.MapPut("/", UpdateDocument)
            .WithName("UpdateLegalDocument")
            .WithSummary("Sets a legal document (admin only)")
            .Produces<LegalDocumentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAdmin();
    }

    private static bool TryParseKind(string kind, out LegalDocumentKind parsed) =>
        Enum.TryParse(kind, ignoreCase: false, out parsed) && Enum.IsDefined(parsed);

    private static IResult UnknownKindProblem() => Results.ValidationProblem(new Dictionary<string, string[]>
    {
        ["kind"] = [$"kind must be one of: {string.Join(", ", Enum.GetNames<LegalDocumentKind>())}."],
    });

    private static async Task<IResult> GetDocument(string kind, AppDbContext db, HttpContext context, CancellationToken ct)
    {
        if (!TryParseKind(kind, out var parsedKind))
            return UnknownKindProblem();

        var doc = await db.LegalDocuments
            .Where(d => d.Kind == parsedKind)
            .Select(d => new { d.Content, d.UpdatedAt, Xmin = EF.Property<uint>(d, "xmin") })
            .FirstOrDefaultAsync(ct);

        if (doc is not null)
            context.Response.Headers.ETag = ConcurrencyToken.ToETag(doc.Xmin);

        return Results.Ok(new LegalDocumentResponse(doc?.Content, doc?.UpdatedAt));
    }

    private static async Task<IResult> UpdateDocument(
        string kind,
        UpdateLegalDocumentRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAuditService audit,
        HttpContext context,
        CancellationToken ct)
    {
        if (!TryParseKind(kind, out var parsedKind))
            return UnknownKindProblem();

        if (!EventOwnership.IsAdmin(principal)) return AdminAccess.Forbid();

        var byteCount = request.Content is null ? 0 : Encoding.UTF8.GetByteCount(request.Content);
        if (byteCount > MaxContentBytes)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Content"] = [$"Content must be {MaxContentBytes:N0} bytes or fewer."],
            });
        }

        var doc = await db.LegalDocuments.FirstOrDefaultAsync(d => d.Kind == parsedKind, ct);
        var beforeContent = doc?.Content;
        if (doc is null)
        {
            doc = new LegalDocument { Kind = parsedKind };
            db.LegalDocuments.Add(doc);
        }
        else
        {
            // A new document has no prior token to match against — If-Match
            // only makes sense (and is only applied) against an existing row.
            ConcurrencyToken.ApplyIfMatch(db, doc, context.Request.Headers.IfMatch);
        }

        doc.Content = request.Content;
        doc.UpdatedAt = DateTime.UtcNow;

        var actorId = EventOwnership.GetUserId(principal);
        audit.Log(db, AuditEventTypes.LegalDocumentUpdated, actorId,
            before: new { Kind = parsedKind.ToString(), Content = AuditContentDigest.Of(beforeContent) },
            after: new { Kind = parsedKind.ToString(), Content = AuditContentDigest.Of(doc.Content) });

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

        context.Response.Headers.ETag = ConcurrencyToken.ToETag(ConcurrencyToken.GetXmin(db, doc));
        return Results.Ok(new LegalDocumentResponse(doc.Content, doc.UpdatedAt));
    }
}
