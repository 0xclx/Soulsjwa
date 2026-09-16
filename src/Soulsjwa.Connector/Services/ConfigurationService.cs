using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Soulsjwa.Connector.Services;

public sealed class ConnectorConfig
{
    public string ServerUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}

/// <summary>
/// On-disk shape of connector-config.json. The API key is stored DPAPI-protected
/// for the current Windows user (<see cref="ProtectedApiKey"/>); a file written
/// by an older build carries it in <see cref="ApiKey"/> as plain text, which is
/// still read so nobody is logged out by the upgrade, and re-written protected
/// on the next save.
/// </summary>
internal sealed class ConnectorConfigFile
{
    public string ServerUrl { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string? ProtectedApiKey { get; set; }
}

public sealed class ConfigurationService
{
    private const string FileName = "connector-config.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _configPath;
    private readonly string _legacyConfigPath;

    public ConfigurationService()
        : this(GetDefaultConfigPath(), GetLegacyConfigPath())
    {
    }

    internal ConfigurationService(string configPath)
        : this(configPath, GetLegacyConfigPath())
    {
    }

    private ConfigurationService(string configPath, string legacyConfigPath)
    {
        _configPath = configPath;
        _legacyConfigPath = legacyConfigPath;
    }

    public static string GetDefaultConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
        {
            appData = AppContext.BaseDirectory;
        }

        return Path.Combine(appData, "Soulsjwa", "Connector", FileName);
    }

    public static string GetLegacyConfigPath() => Path.Combine(AppContext.BaseDirectory, FileName);

    public ConnectorConfig Load()
    {
        var loaded = TryLoadFrom(_configPath);
        if (loaded is not null)
            return loaded;

        // Older connector builds wrote the API key next to the executable. Keep
        // reading that file so existing users are not logged out, but future
        // writes go to the per-user application-data path above.
        if (!string.Equals(_configPath, _legacyConfigPath, StringComparison.Ordinal))
        {
            loaded = TryLoadFrom(_legacyConfigPath);
            if (loaded is not null)
                return loaded;
        }

        return new ConnectorConfig();
    }

    public void Save(ConnectorConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var directory = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var file = new ConnectorConfigFile
        {
            ServerUrl = config.ServerUrl,
            ProtectedApiKey = string.IsNullOrEmpty(config.ApiKey) ? null : Protect(config.ApiKey),
        };
        var json = JsonSerializer.Serialize(file, JsonOptions);
        var tempPath = $"{_configPath}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _configPath, overwrite: true);
    }

    private static ConnectorConfig? TryLoadFrom(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            var file = JsonSerializer.Deserialize<ConnectorConfigFile>(json, JsonOptions);
            if (file is null)
                return new ConnectorConfig();

            return new ConnectorConfig
            {
                ServerUrl = file.ServerUrl,
                ApiKey = file.ProtectedApiKey is { Length: > 0 } protectedKey
                    ? Unprotect(protectedKey)
                    : file.ApiKey ?? string.Empty,
            };
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string Protect(string apiKey) =>
        Convert.ToBase64String(ProtectedData.Protect(
            Encoding.UTF8.GetBytes(apiKey), optionalEntropy: null, DataProtectionScope.CurrentUser));

    /// <summary>
    /// A key protected by another Windows account (or a copied config file)
    /// cannot be unprotected; the user simply has to enter it again, which the
    /// empty key prompts for.
    /// </summary>
    private static string Unprotect(string protectedApiKey)
    {
        try
        {
            var bytes = ProtectedData.Unprotect(
                Convert.FromBase64String(protectedApiKey), optionalEntropy: null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (CryptographicException)
        {
            return string.Empty;
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }
}
