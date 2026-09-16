using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// A non-Development start with AllowedHosts left at "*" (host filtering
/// disabled) must log a warning naming the setting.
/// <para>
/// Configuration comes entirely from process environment variables rather than
/// <c>IWebHostBuilder.ConfigureAppConfiguration</c>: that callback silently
/// does not run once <c>ASPNETCORE_ENVIRONMENT</c> is overridden to a
/// non-Development value, leaving required config missing and the host failing
/// for the wrong reason. Environment variables are read by the standard
/// configuration pipeline regardless of environment.
/// </para>
/// <para>
/// A Development start runs <c>db.Database.Migrate()</c> on the way up, after
/// the AllowedHosts check but still inside the same <c>factory.Server</c> call
/// — so that case provisions a real, empty, uniquely-named database rather than
/// falling through to <c>appsettings.Development.json</c>'s connection string,
/// which may not exist or may carry incompatible migrations.
/// </para>
/// </summary>
[Collection(ConsoleCaptureCollection.Name)]
public class AllowedHostsStartupTests
{
    private static readonly string[] ManagedVariables =
    [
        "ASPNETCORE_ENVIRONMENT", "Jwt__Secret", "Twitch__ClientId",
        "Twitch__ClientSecret", "Twitch__RedirectUri", "Frontend__Url", "AllowedHosts",
        "ConnectionStrings__DefaultConnection",
    ];

    private static async Task<string> RunAndCaptureConsoleAsync(string environment, string allowedHosts)
    {
        Dictionary<string, string?> originalValues = [];
        var originalOut = Console.Out;
        var capture = new ConsoleCapture();
        var databaseName = "soulsjwa_allowedhoststest_" + Guid.NewGuid().ToString("N");
        var provisioned = false;
        var gateHeld = false;
        // The assembly's shared container, not a hardcoded localhost:5432 —
        // the latter only exists on a developer machine running Postgres
        // locally, and fails in CI with "Connection refused".
        var adminConnectionString = await ApiTestBase.SharedAdminConnectionStringAsync();
        var appConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Database = databaseName,
        }.ConnectionString;
        try
        {
            await using (var conn = new NpgsqlConnection(adminConnectionString))
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"CREATE DATABASE \"{databaseName}\"";
                await cmd.ExecuteNonQueryAsync();
                provisioned = true;
            }

            // Exclusive for the whole mutation window: the two values below
            // that differ from the suite's baseline would break any host
            // booting concurrently. See ApiTestBase.HostStartupGate.
            await ApiTestBase.HostStartupGate.WaitAsync();
            gateHeld = true;
            // Snapshotted only once the gate is held: another test may be
            // holding it with its own values set, and capturing those as the
            // "original" would restore them as the suite's baseline.
            originalValues = ManagedVariables.ToDictionary(v => v, Environment.GetEnvironmentVariable);

            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", environment);
            Environment.SetEnvironmentVariable("Jwt__Secret", "test-secret-key-that-is-long-enough-32chars!!");
            Environment.SetEnvironmentVariable("Twitch__ClientId", "test-client-id");
            Environment.SetEnvironmentVariable("Twitch__ClientSecret", "test-client-secret");
            Environment.SetEnvironmentVariable("Twitch__RedirectUri", "http://localhost/api/v1/auth/twitch/callback");
            Environment.SetEnvironmentVariable("Frontend__Url", "http://localhost:5173");
            Environment.SetEnvironmentVariable("AllowedHosts", allowedHosts);
            // Program.cs migrates on a Development start, so this database is
            // deliberately empty rather than cloned from the schema template.
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", appConnectionString);

            using var factory = new WebApplicationFactory<Program>();
            Console.SetOut(capture);
            _ = factory.Server;
        }
        finally
        {
            Console.SetOut(originalOut);
            foreach (var (key, value) in originalValues)
                Environment.SetEnvironmentVariable(key, value);

            // Released only after the environment is back to the baseline, so
            // the next host to build never sees this test's values.
            if (gateHeld) ApiTestBase.HostStartupGate.Release();

            if (provisioned)
            {
                await using var conn = new NpgsqlConnection(adminConnectionString);
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)";
                await cmd.ExecuteNonQueryAsync();
            }
        }

        return capture.ToString();
    }

    [Fact]
    public async Task ProductionWithWildcardAllowedHosts_LogsWarning()
    {
        (await RunAndCaptureConsoleAsync("Production", "*")).Should().Contain("AllowedHosts");
    }

    [Fact]
    public async Task ProductionWithExplicitAllowedHosts_LogsNoWarning()
    {
        (await RunAndCaptureConsoleAsync("Production", "events.example.com")).Should().NotContain("AllowedHosts");
    }

    [Fact]
    public async Task DevelopmentWithWildcardAllowedHosts_LogsNoWarning()
    {
        (await RunAndCaptureConsoleAsync("Development", "*")).Should().NotContain("AllowedHosts");
    }
}
