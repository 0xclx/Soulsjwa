namespace Soulsjwa.Api.Common;

/// <summary>
/// The API's versioned route prefix, centralised so a typo at any of the ~35
/// route-declaration sites can't silently 404 that endpoint.
/// </summary>
public static class ApiRoutes
{
    public const string Prefix = "/api/v1";

    /// <summary>Group base shared by CompletedObjectivesEndpoint, FailedObjectivesEndpoint, and ScoreboardEndpoint.</summary>
    public const string EventById = Prefix + "/events/{eventId:guid}";
}
