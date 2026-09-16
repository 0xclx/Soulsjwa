using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Soulsjwa.Api.Features.TwitchExtension.Services;

/// <summary>
/// Assembles the zip an admin uploads to the Twitch developer console. The
/// front-end files are built once, into the image, by the Dockerfile's
/// frontend stage (<c>npm run build:twitch</c>) and shipped next to the API
/// under <see cref="DefaultDirectory"/>; they contain no deployment-specific
/// value. What makes the zip this deployment's is
/// <see cref="ConfigFileName"/>, written here at download time with the API
/// origin the extension must call — the origin is needed because the bundle
/// runs on Twitch's CDN, and it is written at download time rather than build
/// time so one image serves any hostname.
/// </summary>
public sealed class TwitchExtensionBundle(IConfiguration configuration, IHostEnvironment environment)
{
    /// <summary>Overrides where the built front end lives; defaults to <see cref="DefaultDirectory"/> under the content root.</summary>
    public const string PathKey = "TwitchExtension:BundlePath";

    public const string DefaultDirectory = "twitch-extension";

    /// <summary>The file every entry page loads first; the built bundle ships a placeholder that this replaces.</summary>
    public const string ConfigFileName = "extension-config.js";

    /// <summary>The global the front end reads (<c>src/twitch-extension/api/twitchExtensionClient.ts</c>).</summary>
    public const string ConfigGlobal = "SOULSJWA_TWITCH_EXTENSION";

    public const string ZipFileName = "soulsjwa-twitch-extension.zip";

    /// <summary>Present in every build, so its absence means the bundle is not part of this deployment.</summary>
    private const string MarkerFile = "panel.html";

    public string RootPath =>
        Path.GetFullPath(configuration[PathKey] ?? Path.Combine(environment.ContentRootPath, DefaultDirectory));

    public bool IsAvailable => File.Exists(Path.Combine(RootPath, MarkerFile));

    public int FileCount =>
        IsAvailable ? Directory.EnumerateFiles(RootPath, "*", SearchOption.AllDirectories).Count() : 0;

    /// <summary>What <see cref="ConfigFileName"/> contains for one API origin. JSON-serialised, so the origin can never break out of the script.</summary>
    public static string RenderConfigScript(string apiUrl) =>
        $"window.{ConfigGlobal} = {JsonSerializer.Serialize(new { apiUrl })};\n";

    /// <summary>
    /// Writes the zip: every built file at its relative path (Twitch wants
    /// them at the root), plus the config file for <paramref name="apiUrl"/>.
    /// Assembled in memory first — <see cref="ZipArchive"/> writes its
    /// headers and central directory synchronously, which Kestrel refuses on
    /// a response body — and the bundle is a few hundred kilobytes.
    /// </summary>
    public async Task WriteZipAsync(Stream output, string apiUrl, CancellationToken ct)
    {
        if (!IsAvailable)
            throw new InvalidOperationException($"No extension bundle at {RootPath}.");

        using var buffer = new MemoryStream();
        await AssembleAsync(buffer, apiUrl, ct);
        buffer.Position = 0;
        await buffer.CopyToAsync(output, ct);
    }

    private async Task AssembleAsync(Stream output, string apiUrl, CancellationToken ct)
    {
        var root = RootPath;
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);

        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => (Path: path, Name: Path.GetRelativePath(root, path).Replace('\\', '/')))
            .Where(f => !string.Equals(f.Name, ConfigFileName, StringComparison.Ordinal))
            .OrderBy(f => f.Name, StringComparer.Ordinal);

        foreach (var (path, name) in files)
        {
            var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            await using var target = entry.Open();
            await using var source = File.OpenRead(path);
            await source.CopyToAsync(target, ct);
        }

        var config = zip.CreateEntry(ConfigFileName, CompressionLevel.Optimal);
        await using var configStream = config.Open();
        await configStream.WriteAsync(Encoding.UTF8.GetBytes(RenderConfigScript(apiUrl)), ct);
    }
}
