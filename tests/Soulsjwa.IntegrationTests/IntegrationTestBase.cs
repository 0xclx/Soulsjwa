using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Soulsjwa.Api.Features.Audits.Services;
using Soulsjwa.Api.Features.Games.Services;
using Soulsjwa.Api.Infrastructure.Data;
using Testcontainers.PostgreSql;
using Xunit;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// Base for the integration layer: real components against a real Postgres, no
/// web host and no HTTP. The minimal-API handlers are static methods with
/// explicit dependencies, so tests call them directly.
/// Deliberately not a <c>WebApplicationFactory</c> — that is what once made this
/// project indistinguishable from <c>ApiTests</c>, and it adds a host build to
/// every test here.
/// </summary>
public class IntegrationTestBase : IAsyncLifetime
{
    /// <summary>
    /// Schema-only database each test's own database is cloned from. Nothing
    /// connects to it after setup: Postgres refuses
    /// <c>CREATE DATABASE ... TEMPLATE t</c> while any session is attached to
    /// <c>t</c>.
    /// </summary>
    private const string TemplateDatabase = "soulsjwa_it_template";

    /// <summary>
    /// One Postgres server for the whole assembly, started on first use. xUnit
    /// builds a class instance per test method, so <see cref="InitializeAsync"/>
    /// runs per test — hence a shared server plus a per-test clone of the
    /// template. Never disposed explicitly: Testcontainers' reaper removes it on
    /// process exit, and xUnit v2 has no assembly-level fixture.
    /// </summary>
    private static readonly Lazy<Task<string>> SharedServer =
        new(StartSharedServerAsync, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Set <c>SOULSJWA_TEST_POSTGRES</c> to an admin connection string (a user
    /// that may <c>CREATE DATABASE</c>, pointed at the <c>postgres</c>
    /// maintenance database) to run against an existing server with no Docker.
    /// The template and per-test clones are unchanged. CI leaves it unset and
    /// uses Testcontainers.
    /// </summary>
    private const string ExternalServerVariable = "SOULSJWA_TEST_POSTGRES";

    private static async Task<string> StartSharedServerAsync()
    {
        var external = Environment.GetEnvironmentVariable(ExternalServerVariable);
        if (!string.IsNullOrWhiteSpace(external))
            return await PrepareTemplateAsync(external);

        var container = new PostgreSqlBuilder("postgres:16-alpine")
            // Maintenance database: CREATE/DROP DATABASE cannot run from
            // inside the database being changed.
            .WithDatabase("postgres")
            .WithUsername("postgres")
            .WithPassword("postgres")
            // Durability is already off by default in PostgreSqlBuilder; only
            // the backend count needs raising for parallel test classes.
            .WithCommand("-c", "max_connections=200")
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
        await using (var db = CreateContext(templateConnectionString))
        {
            // MigrateAsync, not EnsureCreatedAsync: EnsureCreated replays only
            // the model snapshot, so raw SQL in a migration never runs — and
            // two things depend on it, InitialCreate's `CREATE EXTENSION
            // pg_trgm` (without which the gin_trgm_ops indexes cannot be
            // created) and the functional index on LOWER("TwitchLogin").
            await db.Database.MigrateAsync();

            // What Program.cs does after migrating. The predefined objective
            // catalogue lives in code, not in a migration, so without this the
            // import endpoints have nothing to import.
            await PredefinedObjectiveSeeder.SeedAsync(db);
        }

        // Migrating leaves pooled connections open to the template, and a
        // single one is enough to fail every clone below.
        await using var templateConnection = new NpgsqlConnection(templateConnectionString);
        NpgsqlConnection.ClearPool(templateConnection);

        return adminConnectionString;
    }

    private static string WithDatabase(string connectionString, string database) =>
        new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = database,
            MaxPoolSize = 10,
        }.ConnectionString;

    private static AppDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private string _adminConnectionString = null!;
    private string _database = null!;
    private readonly List<AppDbContext> _contexts = [];
    private readonly List<ServiceProvider> _providers = [];

    /// <summary>This test's own database. Exposed for the rare test that needs a raw connection.</summary>
    protected string ConnectionString { get; private set; } = null!;

    /// <summary>
    /// The real audit service. It writes through the <see cref="AppDbContext"/>
    /// it is handed, so the audit trail a handler leaves is assertable.
    /// </summary>
    protected static readonly IAuditService Audit = new AuditService();

    protected readonly RecordingCacheStore Cache = new();

    public async Task InitializeAsync()
    {
        _adminConnectionString = await SharedServer.Value;

        _database = $"ittest_{Guid.NewGuid():N}";
        await ExecuteAsync(
            _adminConnectionString,
            $"CREATE DATABASE \"{_database}\" TEMPLATE \"{TemplateDatabase}\"");
        ConnectionString = WithDatabase(_adminConnectionString, _database);
    }

    /// <summary>
    /// A fresh <see cref="AppDbContext"/> on this test's database, disposed at
    /// the end of the test. Call it more than once when the test needs separate
    /// change trackers — proving a constraint is enforced by the database rather
    /// than by EF's identity map, or reading back what another context wrote.
    /// </summary>
    protected AppDbContext CreateDbContext(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString);
        if (interceptors.Length > 0) options.AddInterceptors(interceptors);
        var db = new AppDbContext(options.Options);
        _contexts.Add(db);
        return db;
    }

    /// <summary>
    /// A minimal service provider with <see cref="AppDbContext"/> registered
    /// against this test's database. For components that take an
    /// <see cref="IServiceScopeFactory"/> because they own their unit of work —
    /// background services, mainly.
    /// </summary>
    protected ServiceProvider CreateServiceProvider(
        IEnumerable<KeyValuePair<string, string?>>? settings = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(settings ?? [])
            .Build());
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(ConnectionString));
        var provider = services.BuildServiceProvider();
        _providers.Add(provider);
        return provider;
    }

    public async Task DisposeAsync()
    {
        foreach (var provider in _providers)
            await provider.DisposeAsync();

        foreach (var db in _contexts)
            await db.DisposeAsync();

        // Drop this test's database so a long run does not accumulate one per
        // test. FORCE because a pool may still hold backends open.
        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            NpgsqlConnection.ClearPool(connection);
        }

        try
        {
            await ExecuteAsync(_adminConnectionString, $"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)");
        }
        catch (NpgsqlException)
        {
            // Teardown only — a leftover database dies with the container, and
            // throwing here would mask the test's own result.
        }
    }
}
