using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace Soulsjwa.ApiTests;

/// <summary>
/// Boots the API exactly as a deployment does — <c>ASPNETCORE_ENVIRONMENT</c>
/// set to a non-Development value — against its own migrated database, and
/// hands the caller a client. Everything the suite normally leaves to
/// Development defaults (the sample routes, auto-migration, relaxed CSP, the
/// placeholder JWT secret) is off here, which is the point: a route that only
/// exists in Development cannot be caught by <see cref="ApiTestBase"/>.
///
/// Configuration goes through process environment variables for the reasons
/// documented on <see cref="AllowedHostsStartupTests"/>, so the host is built
/// under <see cref="ApiTestBase.HostStartupGate"/> and the variables are
/// restored before the gate is released. The client keeps working after that:
/// the host read its configuration while being built.
/// </summary>
internal static class ProductionHost
{
    private static readonly string[] ManagedVariables =
    [
        "ASPNETCORE_ENVIRONMENT", "Jwt__Secret", "Twitch__ClientId",
        "Twitch__ClientSecret", "Twitch__RedirectUri", "Frontend__Url", "AllowedHosts",
        "ConnectionStrings__DefaultConnection", "Media__RootPath",
    ];

    public static async Task RunAsync(Func<HttpClient, Task> body, string environment = "Production")
    {
        var connectionString = await ApiTestBase.CreateDatabaseFromTemplateAsync();
        var mediaRoot = Path.Combine(Path.GetTempPath(), "soulsjwa-prodhost-media-" + Guid.NewGuid());
        WebApplicationFactory<Program>? factory = null;
        HttpClient? client = null;
        try
        {
            await ApiTestBase.HostStartupGate.WaitAsync();
            // Snapshotted only once the gate is held: another test may be
            // holding it with its own values set, and capturing those as the
            // "original" would restore them as the suite's baseline.
            var originalValues = ManagedVariables.ToDictionary(v => v, Environment.GetEnvironmentVariable);
            try
            {
                Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", environment);
                Environment.SetEnvironmentVariable("Jwt__Secret", "test-secret-key-that-is-long-enough-32chars!!");
                Environment.SetEnvironmentVariable("Twitch__ClientId", "test-client-id");
                Environment.SetEnvironmentVariable("Twitch__ClientSecret", "test-client-secret");
                Environment.SetEnvironmentVariable("Twitch__RedirectUri", "http://localhost/api/v1/auth/twitch/callback");
                Environment.SetEnvironmentVariable("Frontend__Url", "http://localhost:5173");
                // Explicit rather than "*": the test client sends Host: localhost,
                // and a production start is exactly where filtering is on.
                Environment.SetEnvironmentVariable("AllowedHosts", "localhost");
                Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", connectionString);
                Environment.SetEnvironmentVariable("Media__RootPath", mediaRoot);

                factory = new WebApplicationFactory<Program>();
                client = factory.CreateClient();
            }
            finally
            {
                foreach (var (key, value) in originalValues)
                    Environment.SetEnvironmentVariable(key, value);
                ApiTestBase.HostStartupGate.Release();
            }

            await body(client);
        }
        finally
        {
            client?.Dispose();
            if (factory is not null) await factory.DisposeAsync();
            await ApiTestBase.DropDatabaseAsync(connectionString);
            if (Directory.Exists(mediaRoot)) Directory.Delete(mediaRoot, recursive: true);
        }
    }
}
