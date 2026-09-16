namespace Soulsjwa.Api.Features.Legal.Entities;

/// <summary>
/// A singleton legal document (Impressum or Datenschutz), keyed by
/// <see cref="Kind"/>. Markdown, nullable (no row exists until an admin
/// first sets it) — rendered through the shared <c>renderMarkdown</c> /
/// <c>MarkdownView</c> pipeline, same as <c>EventRules</c>. Legal completeness
/// is deliberately the operator's responsibility: no field validation, no
/// compliance checklist.
/// </summary>
public class LegalDocument
{
    public LegalDocumentKind Kind { get; set; }

    public string? Content { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
