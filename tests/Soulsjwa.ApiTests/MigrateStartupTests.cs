using System.Diagnostics;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// <c>--migrate</c> is what the Compose <c>migrate</c> service and any init
/// container run, with nothing configured but a connection string. It must
/// migrate an empty database and exit 0 without the signing secret or Twitch
/// credentials the serving process insists on — those checks once ran first
/// and made the migrate job fail on every host that had not filled them in.
/// <para>
/// Runs the real entry point as a child process: <c>WebApplicationFactory</c>
/// cannot pass command-line arguments to <c>Program</c>, and the point is the
/// exit path of the process itself. The API's build output sits next to this
/// assembly (it is a project reference), runtimeconfig included.
/// </para>
/// </summary>
public class MigrateStartupTests
{
    [Fact]
    public async Task MigrateOnly_NeedsOnlyAConnectionString_AndExitsZeroAfterMigrating()
    {
        var admin = await ApiTestBase.SharedAdminConnectionStringAsync();
        var database = $"migratetest_{Guid.NewGuid():N}";
        var appConnectionString = new NpgsqlConnectionStringBuilder(admin) { Database = database }.ConnectionString;
        await ExecuteAsync(admin, $"CREATE DATABASE \"{database}\"");
        try
        {
            var apiDll = Path.Combine(AppContext.BaseDirectory, "Soulsjwa.Api.dll");
            File.Exists(apiDll).Should().BeTrue("the API is a project reference of this test assembly");

            var psi = new ProcessStartInfo("dotnet", $"\"{apiDll}\" --migrate")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = AppContext.BaseDirectory,
            };
            // A production start with nothing but the database configured.
            foreach (var key in new[] { "Jwt__Secret", "Twitch__ClientId", "Twitch__ClientSecret", "Twitch__RedirectUri", "Frontend__Url" })
                psi.Environment.Remove(key);
            psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
            psi.Environment["ConnectionStrings__DefaultConnection"] = appConnectionString;

            using var process = Process.Start(psi)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            await process.WaitForExitAsync(cts.Token);

            process.ExitCode.Should().Be(0, "stdout:\n{0}\nstderr:\n{1}", await stdout, await stderr);

            await using var connection = new NpgsqlConnection(appConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT count(*) FROM \"__EFMigrationsHistory\"", connection);
            ((long)(await command.ExecuteScalarAsync())!).Should().BeGreaterThan(0, "the migrate run applied the schema");
        }
        finally
        {
            await ExecuteAsync(admin, $"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)");
        }
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
