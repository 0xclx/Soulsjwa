using System.IO;
using FluentAssertions;
using Soulsjwa.Connector.Services;

namespace Soulsjwa.ConnectorTests;

// ConfigurationService writes connector-config.json to per-user application data.
// The tests below treat that file as test-owned state and clean it up before
// and after each case to keep them isolated.
public class ConfigurationServiceTests : IDisposable
{
    private static readonly string ConfigPath = ConfigurationService.GetDefaultConfigPath();
    private static readonly string LegacyConfigPath = ConfigurationService.GetLegacyConfigPath();

    public ConfigurationServiceTests()
    {
        DeleteConfig();
    }

    public void Dispose()
    {
        DeleteConfig();
    }

    private static void DeleteConfig()
    {
        if (File.Exists(ConfigPath))
            File.Delete(ConfigPath);
        if (File.Exists(LegacyConfigPath))
            File.Delete(LegacyConfigPath);
    }

    [Fact]
    public void Load_WhenFileMissing_ShouldReturnDefaults()
    {
        var service = new ConfigurationService();

        var config = service.Load();

        config.Should().NotBeNull();
        config.ServerUrl.Should().BeEmpty();
        config.ApiKey.Should().BeEmpty();
    }

    [Fact]
    public void SaveThenLoad_ShouldRoundTripValues()
    {
        var service = new ConfigurationService();
        var saved = new ConnectorConfig
        {
            ServerUrl = "https://example.test",
            ApiKey = "secret-key",
        };

        service.Save(saved);
        var loaded = service.Load();

        loaded.ServerUrl.Should().Be(saved.ServerUrl);
        loaded.ApiKey.Should().Be(saved.ApiKey);
    }

    [Fact]
    public void Save_DoesNotWriteTheApiKeyInPlainText()
    {
        var service = new ConfigurationService();

        service.Save(new ConnectorConfig { ServerUrl = "https://example.test", ApiKey = "secret-key" });

        var written = File.ReadAllText(ConfigPath);
        written.Should().NotContain("secret-key");
        written.Should().Contain("ProtectedApiKey");
        service.Load().ApiKey.Should().Be("secret-key");
    }

    [Fact]
    public void Load_WhenAnOlderBuildWroteThePlainTextKey_StillLoadsIt()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        File.WriteAllText(ConfigPath, """{ "ServerUrl": "https://old", "ApiKey": "old-key" }""");

        var loaded = new ConfigurationService().Load();

        loaded.ServerUrl.Should().Be("https://old");
        loaded.ApiKey.Should().Be("old-key");
    }

    [Fact]
    public void Load_WhenTheProtectedKeyCannotBeUnprotected_LeavesTheKeyEmptyRatherThanThrowing()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        File.WriteAllText(ConfigPath, """{ "ServerUrl": "https://x", "ProtectedApiKey": "bm90LWEtZHBhcGktYmxvYg==" }""");

        var loaded = new ConfigurationService().Load();

        loaded.ServerUrl.Should().Be("https://x");
        loaded.ApiKey.Should().BeEmpty();
    }

    [Fact]
    public void Load_WhenFileContainsMalformedJson_ShouldReturnDefaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        File.WriteAllText(ConfigPath, "{ this is not json");
        var service = new ConfigurationService();

        var config = service.Load();

        config.ServerUrl.Should().BeEmpty();
        config.ApiKey.Should().BeEmpty();
    }

    [Fact]
    public void Load_WhenOnlyLegacyExeConfigExists_LoadsItForMigrationCompatibility()
    {
        File.WriteAllText(LegacyConfigPath, """{ "ServerUrl": "https://legacy", "ApiKey": "legacy-key" }""");

        var loaded = new ConfigurationService().Load();

        loaded.ServerUrl.Should().Be("https://legacy");
        loaded.ApiKey.Should().Be("legacy-key");
    }

}
