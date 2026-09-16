namespace Soulsjwa.Api.Common;

/// <summary>
/// Startup reads of numeric settings. <c>int.Parse(config[key] ?? default)</c>
/// crashed the host with a bare <see cref="FormatException"/> that named
/// neither the key nor the value; these name both, so a typo in an
/// environment variable is a one-line fix rather than a stack trace to read.
/// </summary>
public static class ConfigurationValues
{
    public static int ReadPositiveInt(IConfiguration configuration, string key, int defaultValue)
    {
        var raw = configuration[key];
        if (string.IsNullOrWhiteSpace(raw))
            return defaultValue;

        if (!int.TryParse(raw, out var value) || value <= 0)
            throw new InvalidOperationException(
                $"{key} must be a positive integer; got '{raw}'. Unset it to use the default ({defaultValue}).");

        return value;
    }
}
