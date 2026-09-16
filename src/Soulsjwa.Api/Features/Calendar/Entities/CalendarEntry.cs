using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Features.Media.Entities;

namespace Soulsjwa.Api.Features.Calendar.Entities;

/// <summary>
/// An admin/owner-authored calendar entry for one event — a
/// scheduled milestone, announcement, or highlight shown on the global
/// calendar. Readable by everyone; written by the event owner or an admin.
/// </summary>
public class CalendarEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    public string Title { get; set; } = string.Empty;

    /// <summary>Rendered through the shared Markdown pipeline — never raw HTML.</summary>
    public string? DescriptionMarkdown { get; set; }

    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }

    /// <summary>When true, times are ignored and the entry spans whole days.</summary>
    public bool IsAllDay { get; set; }

    public bool IsHighlighted { get; set; }

    /// <summary>A semantic slot name, never a hex value — resolved client-side against the site theme.</summary>
    public CalendarEntryColor Color { get; set; } = CalendarEntryColor.Default;

    public Guid? ImageAssetId { get; set; }
    public MediaAsset? ImageAsset { get; set; }

    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
