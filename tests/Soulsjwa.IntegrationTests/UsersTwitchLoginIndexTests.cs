using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// The case-insensitive TwitchLogin lookups (OAuth upsert, allowlist endpoints,
/// competitor invitation) all compare LOWER("TwitchLogin"), so only a functional
/// index keeps Postgres off a sequential scan. Needs real Postgres — query plans
/// don't exist against the InMemory provider.
/// </summary>
public class UsersTwitchLoginIndexTests : IntegrationTestBase
{
    [Fact]
    public async Task LowerTwitchLoginLookup_UsesTheFunctionalIndex_NotASequentialScan()
    {
        var db = CreateDbContext();

        for (var i = 0; i < 500; i++)
        {
            db.Users.Add(new User
            {
                TwitchId = $"twitch_{i}",
                TwitchLogin = $"User_{i}",
                DisplayName = $"User {i}",
                IsAllowlisted = true,
            });
        }
        await db.SaveChangesAsync();

        await using var conn = new NpgsqlConnection(db.Database.GetConnectionString());
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "EXPLAIN SELECT * FROM \"Users\" WHERE LOWER(\"TwitchLogin\") = @login";
        cmd.Parameters.AddWithValue("login", "user_250");

        var planLines = new List<string>();
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                planLines.Add(reader.GetString(0));
        }
        var plan = string.Join('\n', planLines);

        plan.Should().Contain("IX_Users_TwitchLogin_Lower", "the lookup must use the functional index, not a sequential scan");
        plan.Should().NotContain("Seq Scan on \"Users\"");
    }
}
