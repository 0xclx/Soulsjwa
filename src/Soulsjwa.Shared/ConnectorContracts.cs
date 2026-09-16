namespace Soulsjwa.Shared;

// The response shapes of every /api/v1/connector/* route, in the shared
// assembly so the API serializes and the desktop connector deserializes the
// exact same types. The connector once kept private copies of these; one of
// them drifted (an events list that no longer carried games) and the connector
// could not select a game against any server. Compiling both sides against one
// definition is what prevents that from recurring.

/// <summary>
/// <c>GET /api/v1/connector/version</c>. The connector refuses to proceed when
/// its own <see cref="ConnectorConstants.Version"/> is older than this.
/// </summary>
public sealed record ConnectorVersionResponse(string RequiredVersion);

/// <summary><c>GET /api/v1/connector/supported-games</c> row.</summary>
public sealed record ConnectorSupportedGameResponse(
    int Id,
    string Name,
    string? RequiredConnectorVersion);

/// <summary><c>GET /api/v1/connector/games/{gameId}/data</c>.</summary>
public sealed record ConnectorGameDataResponse(
    int GameId,
    string GameName,
    List<GameDataPoint> DataPoints);

/// <summary>
/// <c>GET /api/v1/connector/events</c> row: an event the authenticated user
/// competes in, with every game the event has so the connector can offer the
/// connector-supported ones. Archived events are never listed.
/// </summary>
/// <param name="IsStarted">
/// Submissions are refused until the organizer starts the event; surfaced so
/// the connector can say so instead of reporting a bare 403.
/// </param>
public sealed record ConnectorEventResponse(
    Guid Id,
    string Name,
    string Description,
    bool IsStarted,
    bool AllowTrialRuns,
    List<ConnectorEventGameResponse> Games);

/// <summary>One game of a <see cref="ConnectorEventResponse"/>.</summary>
/// <param name="ConnectorSupported">
/// True when the game is a known game with connector data definitions; custom
/// games are listed but cannot be monitored.
/// </param>
/// <param name="IsEnabled">
/// Whether the game is the event's currently active one. Official submissions
/// require it; a competitor's own running trial run does not.
/// </param>
public sealed record ConnectorEventGameResponse(
    Guid EventGameId,
    int? KnownGameId,
    string GameName,
    string? KnownGameName,
    bool ConnectorSupported,
    string? RequiredConnectorVersion,
    bool IsEnabled);

/// <summary>
/// <c>POST /api/v1/connector/events/{eventId}/games/{eventGameId}/submit</c>:
/// how many objectives this submission newly completed and newly failed.
/// </summary>
public sealed record ConnectorDataSubmissionResult(
    int CompletedCount,
    int FailedCount);
