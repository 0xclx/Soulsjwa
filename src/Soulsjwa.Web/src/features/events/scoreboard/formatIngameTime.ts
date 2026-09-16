/**
 * Format a duration in milliseconds as in-game time. Renders `H:MM:SS` once the
 * duration reaches an hour and `M:SS` below that, matching the scoreboard's
 * compact IGT column.
 */
export function formatIngameTime(ms: number): string {
  const totalSeconds = Math.floor(ms / 1000)
  const hours = Math.floor(totalSeconds / 3600)
  const minutes = Math.floor((totalSeconds % 3600) / 60)
  const seconds = totalSeconds % 60
  const pad = (n: number) => n.toString().padStart(2, '0')
  return hours > 0 ? `${hours}:${pad(minutes)}:${pad(seconds)}` : `${minutes}:${pad(seconds)}`
}
