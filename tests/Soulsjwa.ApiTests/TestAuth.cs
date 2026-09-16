using Microsoft.Extensions.DependencyInjection;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Infrastructure.Auth;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.ApiTests;

public static class TestAuth
{
    /// <summary>
    /// Returns the raw API key, which is only available at creation time.
    /// Defaults to <see cref="UserRole.Admin"/> because most tests arrange a
    /// user that needs to create or manage events.
    /// </summary>
    public static async Task<(User User, string RawApiKey)> CreateUserWithApiKeyAsync(
        IServiceProvider services, string? twitchLoginPrefix = null, UserRole role = UserRole.Admin)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var login = $"{twitchLoginPrefix ?? "user"}_{suffix}";

        var user = new User
        {
            TwitchId = $"twitch_{suffix}",
            TwitchLogin = login,
            DisplayName = login,
            Role = role,
            IsAllowlisted = true,
        };
        db.Users.Add(user);

        var (rawKey, prefix) = ApiKeyAuthHandler.GenerateApiKey();
        var apiKey = new ApiKey
        {
            UserId = user.Id,
            Name = "test-key",
            KeyHash = ApiKeyAuthHandler.HashApiKey(rawKey),
            KeyPrefix = prefix,
        };
        db.ApiKeys.Add(apiKey);
        await db.SaveChangesAsync();

        return (user, rawKey);
    }

    public static HttpClient CreateAuthenticatedClient(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory,
        string rawApiKey)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", rawApiKey);
        return client;
    }
}
