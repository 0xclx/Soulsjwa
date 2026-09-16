namespace Soulsjwa.Api.Common.Models;

/// <summary>
/// One page of results, either offset-based (<see cref="Page"/>/<see cref="PageSize"/>
/// with a real <see cref="TotalCount"/>) or keyset-based (<see cref="NextCursor"/>
/// set and <see cref="TotalCount"/> possibly null, since counting would defeat the
/// point of avoiding OFFSET on a large table).
/// </summary>
public sealed record PaginatedResponse<T>(
    IReadOnlyList<T> Items,
    int? TotalCount,
    int Page,
    int PageSize,
    string? NextCursor = null)
{
    public bool HasNextPage => TotalCount.HasValue && Page * PageSize < TotalCount.Value;
    public bool HasPreviousPage => Page > 1;
}
