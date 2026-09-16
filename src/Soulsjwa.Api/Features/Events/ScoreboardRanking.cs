using Soulsjwa.Api.Features.Events.Entities;

namespace Soulsjwa.Api.Features.Events;

/// <summary>
/// Shared sort + rank logic for scoreboards / score listings.
/// </summary>
/// <remarks>
/// Sort order is always: total score descending, then lowest in-game time, then
/// earliest real-world completion (the fallback for manual completions, or when
/// one side has no in-game time).
///
/// Ranks then follow <see cref="TieBreakMode"/>: <c>ByTime</c> is strict ordinal
/// (1, 2, 3 …); <c>SharedPlace</c> gives equal scores the same rank by standard
/// competition ranking (1, 1, 3 …), ignoring completion times.
/// </remarks>
public interface IScoreboardSortable
{
    int TotalScore { get; }
    long? TotalInGameTimeMs { get; }
    DateTime? LastCompletedAt { get; }
}

public static class ScoreboardRanking
{
    public static List<T> Sort<T>(IEnumerable<T> entries) where T : IScoreboardSortable =>
        entries
            .OrderByDescending(e => e.TotalScore)
            // long.MaxValue / DateTime.MaxValue push entries without a time
            // to the back so a present time always beats an absent one.
            .ThenBy(e => e.TotalInGameTimeMs ?? long.MaxValue)
            .ThenBy(e => e.LastCompletedAt ?? DateTime.MaxValue)
            .ToList();

    /// <summary>
    /// Assigns 1-based ranks to a <i>pre-sorted</i> list. Shared-place ties are
    /// determined by score equality and resolved per <paramref name="mode"/>.
    /// </summary>
    public static int[] AssignRanks<T>(IReadOnlyList<T> sortedEntries, TieBreakMode mode)
        where T : IScoreboardSortable
    {
        var ranks = new int[sortedEntries.Count];
        if (sortedEntries.Count == 0) return ranks;

        if (mode == TieBreakMode.SharedPlace)
        {
            // Standard competition ranking ("1224"): two tied at #1 are both 1,
            // the next is 3.
            var currentRank = 1;
            ranks[0] = 1;
            for (var i = 1; i < sortedEntries.Count; i++)
            {
                if (sortedEntries[i].TotalScore == sortedEntries[i - 1].TotalScore)
                {
                    ranks[i] = currentRank;
                }
                else
                {
                    currentRank = i + 1;
                    ranks[i] = currentRank;
                }
            }
            return ranks;
        }

        // ByTime — strict ordinal.
        for (var i = 0; i < sortedEntries.Count; i++) ranks[i] = i + 1;
        return ranks;
    }

}
