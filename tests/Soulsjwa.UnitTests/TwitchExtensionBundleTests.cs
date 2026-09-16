using System.IO.Compression;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Soulsjwa.Api.Features.TwitchExtension.Services;
using Xunit;

namespace Soulsjwa.UnitTests;

/// <summary>
/// The zip an admin uploads to Twitch: every built file at its relative
/// path, plus the config file carrying this deployment's API origin.
/// </summary>
public sealed class TwitchExtensionBundleTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"twitch-bundle-{Guid.NewGuid():N}");

    private sealed class Env : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private TwitchExtensionBundle Bundle(string? configuredPath) =>
        new(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { [TwitchExtensionBundle.PathKey] = configuredPath })
                .Build(),
            new Env { ContentRootPath = _root });

    private void WriteBuiltBundle()
    {
        Directory.CreateDirectory(Path.Combine(_root, "assets"));
        File.WriteAllText(Path.Combine(_root, "panel.html"), "<html>panel</html>");
        File.WriteAllText(Path.Combine(_root, "assets", "mount-abc.js"), "console.log('mount')");
        File.WriteAllText(Path.Combine(_root, TwitchExtensionBundle.ConfigFileName), "window.PLACEHOLDER = 1;");
    }

    [Fact]
    public async Task WritesEveryFileAtItsRelativePath_AndTheConfigForTheGivenOrigin()
    {
        WriteBuiltBundle();
        var bundle = Bundle(_root);
        using var output = new MemoryStream();

        bundle.IsAvailable.Should().BeTrue();
        bundle.FileCount.Should().Be(3);
        await bundle.WriteZipAsync(output, "https://events.example.com", CancellationToken.None);

        output.Position = 0;
        using var zip = new ZipArchive(output, ZipArchiveMode.Read);
        zip.Entries.Select(e => e.FullName).Should().BeEquivalentTo(
            ["panel.html", "assets/mount-abc.js", TwitchExtensionBundle.ConfigFileName]);
        using var reader = new StreamReader(zip.GetEntry(TwitchExtensionBundle.ConfigFileName)!.Open());
        var config = await reader.ReadToEndAsync();
        config.Should().Be(TwitchExtensionBundle.RenderConfigScript("https://events.example.com"));
        config.Should().Contain($"window.{TwitchExtensionBundle.ConfigGlobal} = ")
            .And.Contain("\"apiUrl\":\"https://events.example.com\"");
    }

    [Fact]
    public void RenderConfigScript_NeverLetsTheOriginBreakOutOfTheScript()
    {
        var script = TwitchExtensionBundle.RenderConfigScript("https://x\"</script><script>alert(1)");

        script.Should().NotContain("</script>");
        script.Should().NotContain("\"</");
    }

    [Fact]
    public void FallsBackToTheContentRootDirectory_WhenNoPathIsConfigured()
    {
        var bundle = Bundle(null);

        bundle.RootPath.Should().Be(Path.Combine(_root, TwitchExtensionBundle.DefaultDirectory));
        bundle.IsAvailable.Should().BeFalse();
        bundle.FileCount.Should().Be(0);
    }

    [Fact]
    public async Task WithoutABuiltBundle_WritingThrows()
    {
        var bundle = Bundle(_root);

        var act = () => bundle.WriteZipAsync(new MemoryStream(), "https://events.example.com", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
