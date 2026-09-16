namespace Soulsjwa.Api.Features.Events.Entities;

/// <summary>
/// An event's rules document — 1:1 with <see cref="Event"/>. Markdown; no row
/// exists until an owner/admin first sets it. Rendered through the shared
/// <c>renderMarkdown</c>/<c>MarkdownView</c> pipeline, so it carries the same
/// XSS guarantees as every other Markdown surface.
/// </summary>
public class EventRules
{
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    public string? Content { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
