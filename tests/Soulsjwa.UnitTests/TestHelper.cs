using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.UnitTests;

public static class TestHelper
{
    /// <summary>
    /// An <see cref="AppDbContext"/> for constructors that demand one in a
    /// test that never touches it (the JWT access-token path). No provider is
    /// configured, so any query throws — which is the point: a unit test that
    /// needs a database belongs in <c>Soulsjwa.IntegrationTests</c>, on real
    /// Postgres. The InMemory provider that used to live here is gone; it
    /// enforced no constraints, no transactions and no SQL translation, and
    /// production code carried <c>IsRelational()</c> guards just to run on it.
    /// </summary>
    public static AppDbContext CreateUnusedDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().Options);
}
