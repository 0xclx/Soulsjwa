using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// The host must refuse to start rather than boot with a signing key an
/// attacker could forge offline. Does not derive from <see cref="ApiTestBase"/>
/// (the failure happens before the DB is touched) but still takes
/// <see cref="ApiTestBase.HostStartupGate"/>, since it configures the host
/// through process-wide environment variables.
/// <para>
/// Configuration comes from environment variables rather than
/// <c>ConfigureAppConfiguration</c>, which does not reliably reach
/// <c>Program.cs</c>'s own top-level config reads (see
/// <see cref="AllowedHostsStartupTests"/>) and would silently leave the
/// too-short secret out of what the app validates.
/// <c>ConnectionStrings__DefaultConnection</c> points at an unreachable host so
/// that, if the Jwt:Secret check were ever moved after the startup migration,
/// this test fails loudly instead of migrating a real database.
/// </para>
/// </summary>
public class JwtSecretStartupTests
{
    private static readonly string[] ManagedVariables =
    [
        "Jwt__Secret",
        "Twitch__ClientId",
        "Twitch__ClientSecret",
        "Twitch__RedirectUri",
        "Frontend__Url",
        "ConnectionStrings__DefaultConnection",
    ];

    [Fact]
    public async Task ShortJwtSecret_FailsHostStartup()
    {
        // Exclusive for the whole mutation window: a host booting concurrently
        // would read this test's too-short secret and fail for the wrong
        // reason. Conversely, ApiTestBase's static constructor sets the suite's
        // long Jwt__Secret baseline on first touch of that type — touching
        // HostStartupGate here forces it to run BEFORE the assignments below
        // rather than during them, where it would overwrite the short secret.
        await ApiTestBase.HostStartupGate.WaitAsync();
        var originalValues = ManagedVariables.ToDictionary(v => v, Environment.GetEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable("Jwt__Secret", "too-short");
            Environment.SetEnvironmentVariable("Twitch__ClientId", "test-client-id");
            Environment.SetEnvironmentVariable("Twitch__ClientSecret", "test-client-secret");
            Environment.SetEnvironmentVariable("Twitch__RedirectUri", "http://localhost/api/v1/auth/twitch/callback");
            Environment.SetEnvironmentVariable("Frontend__Url", "http://localhost:5173");
            Environment.SetEnvironmentVariable(
                "ConnectionStrings__DefaultConnection",
                "Host=unreachable.invalid;Database=x;Username=x;Password=x");

            using var factory = new WebApplicationFactory<Program>();
            var act = () => factory.Server;

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*32*");
        }
        finally
        {
            foreach (var (key, value) in originalValues)
                Environment.SetEnvironmentVariable(key, value);

            // Released only once the environment is back to the baseline, so
            // the next host to build never sees this test's values.
            ApiTestBase.HostStartupGate.Release();
        }
    }
}
