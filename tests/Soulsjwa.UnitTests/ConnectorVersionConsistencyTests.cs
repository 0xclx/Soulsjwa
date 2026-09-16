using System.Text.Json;
using FluentAssertions;
using Soulsjwa.Shared;
using Xunit;

namespace Soulsjwa.UnitTests;

/// <summary>
/// The connector version has one source, <see cref="ConnectorConstants.Version"/>.
/// Everything else that carries a version must agree with it or stay below it.
/// </summary>
public class ConnectorVersionConsistencyTests
{
    [Fact]
    public void ConnectorVersion_IsAThreePartVersion()
    {
        var parsed = Version.Parse(ConnectorConstants.Version);

        parsed.Build.Should().BeGreaterThanOrEqualTo(0, "the connector compares major.minor.patch");
        parsed.Revision.Should().Be(-1, "a fourth component would never match the csproj-derived assembly version");
    }

    [Fact]
    public void NoSeededGame_RequiresAConnectorNewerThanTheOneThatShips()
    {
        var seedPath = FindInRepo(Path.Combine("src", "Soulsjwa.Api", "Infrastructure", "Data", "Seed", "games.json"));
        using var document = JsonDocument.Parse(File.ReadAllText(seedPath));
        var shipped = Version.Parse(ConnectorConstants.Version);

        foreach (var game in document.RootElement.EnumerateArray())
        {
            if (!game.TryGetProperty("requiredConnectorVersion", out var required) || required.ValueKind == JsonValueKind.Null)
                continue;
            var requiredVersion = Version.Parse(required.GetString()!);
            requiredVersion.Should().BeLessThanOrEqualTo(shipped,
                "game {0} would otherwise refuse every connector that exists", game.GetProperty("name").GetString());
        }
    }

    private static string FindInRepo(string relativePath)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException($"{relativePath} not found above {AppContext.BaseDirectory}");
    }
}
