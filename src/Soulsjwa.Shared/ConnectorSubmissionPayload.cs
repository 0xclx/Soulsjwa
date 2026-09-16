namespace Soulsjwa.Shared;

/// <summary>
/// Body of <c>POST /api/v1/connector/events/{eventId}/games/{gameId}/submit</c>.
/// The user is derived from the authenticated API key — it is intentionally NOT
/// part of this payload to prevent spoofing other competitors.
/// </summary>
/// <param name="Data">
/// JSON object as a string mapping <see cref="GameDataPoint.Id"/> values to
/// the value read from the running game's memory via SoulMemory (e.g.
/// <c>{"100":1,"111":0,"death_count":42,"game_time":12345}</c>).
/// </param>
public sealed record ConnectorSubmissionPayload(string Data);
