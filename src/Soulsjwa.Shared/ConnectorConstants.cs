namespace Soulsjwa.Shared;

public static class ConnectorConstants
{
    /// <summary>
    /// The one place the connector version lives. Bump it when supported game
    /// data or the submission contract changes, so connectors are forced to
    /// update to the latest definitions. Soulsjwa.Connector.csproj reads this
    /// line at build time for its assembly version and the download's zip
    /// filename, so keep the <c>Version = "..."</c> shape intact. A game's
    /// <c>requiredConnectorVersion</c> in games.json may never exceed it (a
    /// unit test enforces that).
    /// </summary>
    public const string Version = "3.3.0";
}

/// <summary>
/// Canonical seed-data GameId values shared between the API and the connector.
/// Keep in sync with <c>src/Soulsjwa.Api/Infrastructure/Data/Seed/games.json</c>.
/// </summary>
public static class GameIds
{
    public const int DarkSouls1Remastered = 1;

    public const int DarkSouls2Scholar = 2;

    public const int DarkSouls3 = 3;

    public const int Sekiro = 6;

    public const int EldenRingMemory = 9;
}
