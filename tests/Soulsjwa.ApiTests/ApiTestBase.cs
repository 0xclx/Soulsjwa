using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Soulsjwa.Api.Infrastructure.Data;
using Testcontainers.PostgreSql;
using Xunit;

namespace Soulsjwa.ApiTests;

public class ApiTestBase : IAsyncLifetime
{
    /// <summary>
    /// Jwt/Twitch/Frontend config the app validates at startup, before
    /// <c>WebApplicationFactory</c>'s <c>ConfigureAppConfiguration</c> hook
    /// can reach it — see the static constructor below.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> EnvironmentDefaults = new Dictionary<string, string>
    {
        ["Jwt__Secret"] = "test-secret-key-that-is-long-enough-32chars!!",
        ["Twitch__ClientId"] = "test-client-id",
        ["Twitch__ClientSecret"] = "test-client-secret",
        ["Twitch__RedirectUri"] = "http://localhost/api/v1/auth/twitch/callback",
        ["Frontend__Url"] = "http://localhost:5173",
        ["TwitchExtension__ClientId"] = TwitchExtensionTestConfig.ClientId,
        ["TwitchExtension__Secret"] = TwitchExtensionTestConfig.SecretBase64,
        ["TwitchExtension__BundlePath"] = TwitchExtensionTestConfig.BundlePath,
    };

    /// <summary>
    /// Sets the shared Jwt/Twitch/Frontend baseline once per test process, via
    /// environment variables rather than <c>ConfigureAppConfiguration</c>.
    /// <para>
    /// Program.cs reads these values off <c>builder.Configuration</c> in its
    /// synchronous top-level code, before <c>builder.Build()</c> — the
    /// Jwt:Secret strength check, the Twitch config check, <c>AddJwtBearer</c>'s
    /// <c>TokenValidationParameters</c> and the CORS allowed origin. A
    /// <c>ConfigureAppConfiguration</c> override registered via
    /// <c>WithWebHostBuilder</c> does not reach those reads, though it does
    /// reach anything resolved later from the DI-registered
    /// <c>IConfiguration</c> — a split that silently signed tokens (minted via
    /// a DI-resolved <c>JwtTokenService</c>) with a different secret than the
    /// bearer scheme validated them with, so every such token 401'd.
    /// Environment variables avoid it: <c>WebApplication.CreateBuilder</c>
    /// reads them before any customization hook runs, so every reader sees the
    /// same value.
    /// </para>
    /// <para>
    /// Never clear or restore these — the set is process-wide and idempotent,
    /// and test classes run concurrently.
    /// </para>
    /// </summary>
    static ApiTestBase()
    {
        foreach (var (key, value) in EnvironmentDefaults)
            Environment.SetEnvironmentVariable(key, value);
    }

    /// <summary>
    /// Schema-only database every test's own database is cloned from. Nothing
    /// may connect to it afterwards: Postgres refuses
    /// <c>CREATE DATABASE ... TEMPLATE t</c> while any session is connected to
    /// <c>t</c>.
    /// </summary>
    private const string TemplateDatabase = "soulsjwa_template";

    /// <summary>
    /// One Postgres server for the whole test assembly, started on first use.
    ///
    /// xUnit constructs a test class instance per test *method*, so
    /// <see cref="InitializeAsync"/> runs once per test; starting a container
    /// there was the dominant cost of the suite. One server plus a per-test
    /// <c>CREATE DATABASE ... TEMPLATE</c> gives identical isolation — every
    /// test still gets a virgin schema — far more cheaply.
    ///
    /// <see cref="Lazy{T}"/> of a task rather than a lock, so concurrent
    /// classes all await the same start. The container is never explicitly
    /// disposed: Testcontainers' resource reaper removes it at process exit,
    /// the only hook available given xUnit v2 has no assembly-level fixture.
    /// </summary>
    private static readonly Lazy<Task<string>> SharedServer =
        new(StartSharedServerAsync, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Admin connection string for the assembly's shared Postgres, starting it
    /// if needed — for test classes that do not derive from this type but still
    /// need a real server (see <see cref="AllowedHostsStartupTests"/>).
    /// </summary>
    internal static Task<string> SharedAdminConnectionStringAsync() => SharedServer.Value;

    /// <summary>
    /// Serializes host construction across the assembly.
    ///
    /// <see cref="AllowedHostsStartupTests"/> can only configure a host through
    /// process-wide environment variables, and two of the values it sets —
    /// <c>ASPNETCORE_ENVIRONMENT=Production</c> and an explicit
    /// <c>AllowedHosts</c> — would break any other host booting inside its
    /// window: host filtering rejects <c>Host: localhost</c> with 400.
    ///
    /// Held only while a host is being built, not for the test body.
    /// </summary>
    internal static readonly SemaphoreSlim HostStartupGate = new(1, 1);

    /// <summary>
    /// Points the suite at an already-running Postgres instead of starting a
    /// container, so Docker is not needed: set <c>SOULSJWA_TEST_POSTGRES</c> to
    /// an admin connection string (a user that may <c>CREATE DATABASE</c>,
    /// pointed at the <c>postgres</c> maintenance database). Everything else is
    /// unchanged — each test still gets its own cloned database. CI leaves it
    /// unset and uses Testcontainers.
    /// </summary>
    internal const string ExternalServerVariable = "SOULSJWA_TEST_POSTGRES";

    private static async Task<string> StartSharedServerAsync()
    {
        var external = Environment.GetEnvironmentVariable(ExternalServerVariable);
        if (!string.IsNullOrWhiteSpace(external))
            return await PrepareTemplateAsync(external);

        var container = new PostgreSqlBuilder("postgres:16-alpine")
            // The maintenance database: CREATE/DROP DATABASE has to run from a
            // connection that is not itself inside the database being changed.
            .WithDatabase("postgres")
            .WithUsername("postgres")
            .WithPassword("postgres")
            // PostgreSqlBuilder.Init() already passes fsync=off,
            // synchronous_commit=off and full_page_writes=off, so durability
            // tuning is not ours to add. This is: one server serves every class
            // running in parallel, each with its own pool, and the default 100
            // backends is not enough for maxParallelThreads x MaxPoolSize.
            .WithCommand("-c", "max_connections=400")
            .Build();

        await container.StartAsync();

        return await PrepareTemplateAsync(container.GetConnectionString());
    }

    private static async Task<string> PrepareTemplateAsync(string adminConnectionString)
    {
        // Dropped first for the external-server case: a previous run's
        // template would otherwise be reused, schema and all, and quietly
        // outlive the migration that should have changed it.
        await ExecuteAsync(adminConnectionString, $"DROP DATABASE IF EXISTS \"{TemplateDatabase}\" WITH (FORCE)");
        await ExecuteAsync(adminConnectionString, $"CREATE DATABASE \"{TemplateDatabase}\"");

        var templateConnectionString = WithDatabase(adminConnectionString, TemplateDatabase);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(templateConnectionString)
            .Options;
        await using (var db = new AppDbContext(options))
        {
            // MigrateAsync, not EnsureCreatedAsync. EnsureCreated replays only
            // the model snapshot, so raw SQL in a migration never runs — and
            // InitialCreate's `CREATE EXTENSION IF NOT EXISTS pg_trgm` is raw
            // SQL. Without it the two GIN indexes using the gin_trgm_ops
            // operator class cannot be created, failing schema creation with
            // `42704: operator class "gin_trgm_ops" does not exist`.
            //
            // Migrating also keeps the test schema matching production exactly
            // (including the functional index on LOWER("TwitchLogin")), and is
            // affordable because it happens once per assembly.
            await db.Database.MigrateAsync();
        }

        // Migrating leaves pooled connections open to the template, and a
        // single one of those is enough to fail every clone below.
        await using var templateConnection = new NpgsqlConnection(templateConnectionString);
        NpgsqlConnection.ClearPool(templateConnection);

        return adminConnectionString;
    }

    /// <summary>
    /// A fresh, migrated database of its own for a test that builds a host
    /// outside this base class (see <see cref="ProductionHost"/>). Returns the
    /// application connection string; hand it back to
    /// <see cref="DropDatabaseAsync"/> when done.
    /// </summary>
    internal static async Task<string> CreateDatabaseFromTemplateAsync()
    {
        var admin = await SharedServer.Value;
        var database = $"apitest_{Guid.NewGuid():N}";
        await ExecuteAsync(admin, $"CREATE DATABASE \"{database}\" TEMPLATE \"{TemplateDatabase}\"");
        return WithDatabase(admin, database);
    }

    internal static async Task DropDatabaseAsync(string connectionString)
    {
        var admin = await SharedServer.Value;
        var database = new NpgsqlConnectionStringBuilder(connectionString).Database;
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            NpgsqlConnection.ClearPool(connection);
        }
        await ExecuteAsync(admin, $"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)");
    }

    private static string WithDatabase(string connectionString, string database) =>
        new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = database,
            // Each test gets its own pool; without a cap, a few dozen parallel
            // classes can ask for more backends than the server allows.
            MaxPoolSize = 10,
        }.ConnectionString;

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    protected WebApplicationFactory<Program> Factory = null!;
    protected HttpClient Client = null!;

    private string _adminConnectionString = null!;
    private string _database = null!;

    /// <summary>
    /// Exposes this test's own database so tests needing a differently
    /// configured DbContext (e.g. with a command-counting interceptor) can
    /// build their own factory against the same data.
    /// </summary>
    protected string PostgresConnectionString { get; private set; } = null!;

    private readonly string _mediaRoot = Path.Combine(Path.GetTempPath(), "soulsjwa-apitests-media-" + Guid.NewGuid());

    /// <summary>
    /// A throwaway web root holding a minimal SPA shell.
    ///
    /// <c>Program.cs</c> ends with <c>MapFallbackToFile("index.html")</c>, but
    /// that file is an uncommitted frontend build artifact and the backend CI
    /// job never builds the frontend, so the fallback would answer 404. Owning
    /// the web root here makes it behave as it does in production without
    /// committing a placeholder shell that would then ship.
    /// </summary>
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), "soulsjwa-apitests-webroot-" + Guid.NewGuid());

    public async Task InitializeAsync()
    {
        _adminConnectionString = await SharedServer.Value;

        // Postgres identifiers cap at 63 bytes; this is 34.
        _database = $"apitest_{Guid.NewGuid():N}";
        await ExecuteAsync(
            _adminConnectionString,
            $"CREATE DATABASE \"{_database}\" TEMPLATE \"{TemplateDatabase}\"");
        PostgresConnectionString = WithDatabase(_adminConnectionString, _database);

        Directory.CreateDirectory(_webRoot);
        await File.WriteAllTextAsync(
            Path.Combine(_webRoot, "index.html"),
            "<!doctype html><title>test shell</title>");

        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseWebRoot(_webRoot);

                // Media:RootPath is per-instance and read lazily via
                // DI-injected IConfiguration, well after startup — so unlike
                // Jwt/Twitch/Frontend, ConfigureAppConfiguration does reach it.
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Media:RootPath"] = _mediaRoot,
                    });
                });

                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                    if (descriptor != null) services.Remove(descriptor);

                    services.AddDbContext<AppDbContext>(options =>
                        options.UseNpgsql(PostgresConnectionString));

                    // Drop the retention timer loop: the one test covering
                    // retention constructs RetentionService directly, and
                    // letting the loop run only adds background queries against
                    // a database the test is asserting on.
                    //
                    // Targeted by implementation type, NOT
                    // RemoveAll<IHostedService>(): the web server itself is
                    // started by an IHostedService (GenericWebHostService), so
                    // clearing them all leaves a host that serves nothing.
                    foreach (var hosted in services
                        .Where(d => d.ServiceType == typeof(IHostedService)
                            && d.ImplementationType == typeof(RetentionService))
                        .ToList())
                    {
                        services.Remove(hosted);
                    }
                });
            });

        // CreateClient is what actually builds the host, so the gate has to
        // cover it rather than just the factory's construction.
        await HostStartupGate.WaitAsync();
        try
        {
            Factory = factory;
            Client = Factory.CreateClient();
        }
        finally
        {
            HostStartupGate.Release();
        }
    }

    /// <summary>
    /// Waits for the request-logging middleware to flush at least one line into
    /// a <see cref="Console.Out"/> capture, rather than sleeping a duration
    /// picked to be "probably enough". Returns whatever the capture holds on
    /// timeout, so the caller's own assertion produces the failure message.
    /// </summary>
    protected static async Task<string> WaitForCapturedOutputAsync(
        ConsoleCapture capture, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (true)
        {
            var text = capture.ToString();
            if (!string.IsNullOrWhiteSpace(text) || DateTime.UtcNow >= deadline) return text;
            await Task.Delay(10);
        }
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();

        // FORCE because the factory's pool may still hold backends open even
        // after disposal.
        await using (var connection = new NpgsqlConnection(PostgresConnectionString))
        {
            NpgsqlConnection.ClearPool(connection);
        }

        try
        {
            await ExecuteAsync(_adminConnectionString, $"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)");
        }
        catch (NpgsqlException)
        {
            // Teardown only — a database left behind dies with the container,
            // and failing here would mask the test's own result.
        }

        if (Directory.Exists(_mediaRoot))
            Directory.Delete(_mediaRoot, recursive: true);
        if (Directory.Exists(_webRoot))
            Directory.Delete(_webRoot, recursive: true);
    }
}
