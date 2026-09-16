using System.Data;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Diagnostics;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Infrastructure.Data;

using Soulsjwa.Api.Features.Users;

namespace Soulsjwa.Api.Infrastructure.Auth;

public class TwitchAuthService(
    HttpClient httpClient,
    IConfiguration configuration,
    AppDbContext dbContext,
    ILogger<TwitchAuthService> logger)
{
    /// <summary>Caps how much of a Twitch response body is buffered — a success status is not a guarantee of a small or well-formed body.</summary>
    private const int MaxUserInfoResponseBytes = 64 * 1024;

    private readonly string _clientId = configuration["Twitch:ClientId"] ?? throw new InvalidOperationException("Twitch ClientId not configured");
    private readonly string _clientSecret = configuration["Twitch:ClientSecret"] ?? throw new InvalidOperationException("Twitch ClientSecret not configured");
    private readonly string _redirectUri = configuration["Twitch:RedirectUri"] ?? throw new InvalidOperationException("Twitch RedirectUri not configured");

    public string GetAuthorizationUrl(string state)
    {
        using var operation = DiagnosticsConfig.StartBusinessOperation(
            DiagnosticsConfig.BusinessOperationNames.TwitchAuthorizationUrl,
            DiagnosticsConfig.ActivityNames.TwitchAuthorizationUrl);
        operation.SetTag(DiagnosticsConfig.Tags.OAuthProvider, DiagnosticsConfig.Providers.Twitch);
        var scopes = "user:read:email";
        var url = $"https://id.twitch.tv/oauth2/authorize" +
               $"?client_id={Uri.EscapeDataString(_clientId)}" +
               $"&redirect_uri={Uri.EscapeDataString(_redirectUri)}" +
               $"&response_type=code" +
               $"&scope={Uri.EscapeDataString(scopes)}" +
               $"&state={Uri.EscapeDataString(state)}";
        logger.TwitchAuthorizationUrlGenerated(scopes);
        return url;
    }

    public async Task<TwitchTokenResponse?> ExchangeCodeAsync(string code, CancellationToken ct = default)
    {
        using var operation = DiagnosticsConfig.StartBusinessOperation(
            DiagnosticsConfig.BusinessOperationNames.TwitchExchangeCode,
            DiagnosticsConfig.ActivityNames.TwitchExchangeCode);
        operation.SetTag(DiagnosticsConfig.Tags.OAuthProvider, DiagnosticsConfig.Providers.Twitch);
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = _redirectUri,
        });

        var response = await httpClient.PostAsync("https://id.twitch.tv/oauth2/token", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.TwitchCodeExchangeFailed(response.StatusCode);
            operation.SetError(DiagnosticsConfig.OperationStatuses.Failure, response.StatusCode.ToString());
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        var token = JsonSerializer.Deserialize<TwitchTokenResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        logger.TwitchCodeExchanged();
        return token;
    }

    public async Task<TwitchUserInfo?> GetUserInfoAsync(string accessToken, CancellationToken ct = default)
    {
        using var operation = DiagnosticsConfig.StartBusinessOperation(
            DiagnosticsConfig.BusinessOperationNames.TwitchGetUserInfo,
            DiagnosticsConfig.ActivityNames.TwitchGetUserInfo);
        operation.SetTag(DiagnosticsConfig.Tags.OAuthProvider, DiagnosticsConfig.Providers.Twitch);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.twitch.tv/helix/users");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Client-Id", _clientId);

        var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.TwitchUserInfoFailed(response.StatusCode);
            operation.SetError(DiagnosticsConfig.OperationStatuses.Failure, response.StatusCode.ToString());
            return null;
        }

        string json;
        try
        {
            json = await ReadBoundedAsync(response.Content, MaxUserInfoResponseBytes, ct);
        }
        catch (InvalidOperationException)
        {
            logger.TwitchUserInfoMalformed();
            operation.SetError(DiagnosticsConfig.OperationStatuses.Failure, DiagnosticsConfig.OperationStatuses.InvalidJson);
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.GetArrayLength() == 0)
            {
                logger.TwitchUserInfoEmpty();
                operation.SetStatus(DiagnosticsConfig.OperationStatuses.Empty);
                return null;
            }

            var user = data[0];
            var userInfo = new TwitchUserInfo(
                user.GetProperty("id").GetString() ?? string.Empty,
                user.GetProperty("login").GetString() ?? string.Empty,
                user.GetProperty("display_name").GetString() ?? string.Empty,
                user.TryGetProperty("email", out var email) ? email.GetString() : null,
                user.TryGetProperty("profile_image_url", out var pic) ? pic.GetString() : null
            );
            operation.SetTag(DiagnosticsConfig.Tags.TwitchUserId, userInfo.Id);
            logger.TwitchUserInfoRetrieved(userInfo.Login, userInfo.Id);
            return userInfo;
        }
        catch (JsonException)
        {
            logger.TwitchUserInfoMalformed();
            operation.SetError(DiagnosticsConfig.OperationStatuses.Failure, DiagnosticsConfig.OperationStatuses.InvalidJson);
            return null;
        }
        catch (KeyNotFoundException)
        {
            logger.TwitchUserInfoMalformed();
            operation.SetError(DiagnosticsConfig.OperationStatuses.Failure, DiagnosticsConfig.OperationStatuses.InvalidJson);
            return null;
        }
    }

    /// <summary>Throws <see cref="InvalidOperationException"/> rather than buffering a body past <paramref name="maxBytes"/>.</summary>
    private static async Task<string> ReadBoundedAsync(HttpContent content, int maxBytes, CancellationToken ct)
    {
        await using var stream = await content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > maxBytes)
                throw new InvalidOperationException($"Response body exceeded {maxBytes} bytes.");
            buffer.Write(chunk, 0, read);
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    public async Task<User> UpsertUserAsync(TwitchUserInfo twitchUser, CancellationToken ct = default)
    {
        using var operation = DiagnosticsConfig.StartBusinessOperation(
            DiagnosticsConfig.BusinessOperationNames.TwitchUpsertUser,
            DiagnosticsConfig.ActivityNames.TwitchUpsertUser);
        operation.SetTags(
            (DiagnosticsConfig.Tags.TwitchUserId, twitchUser.Id),
            (DiagnosticsConfig.Tags.TwitchLogin, twitchUser.Login));
        var loginLower = twitchUser.Login.ToLowerInvariant();

        // Bootstrap-admin promotion runs inside a RepeatableRead transaction so
        // the reads below (existing user, "any admin yet", allowlist) come from
        // one consistent snapshot. Note what it does NOT do: Postgres's
        // RepeatableRead is snapshot isolation, so two concurrent first-time
        // sign-ins can both observe `anyAdminExists == false`. Two admins still
        // cannot result — only the one configured login is ever promoted, and
        // the unique index on Users.TwitchId makes the second concurrent insert
        // of that login fail rather than create a second row.
        await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        try
        {
            var user = await dbContext.Users
                .FirstOrDefaultAsync(u => u.TwitchId == twitchUser.Id, ct);

            // No row for the real Twitch id — an admin may have pre-created a
            // placeholder row for this login (invited as a competitor by handle
            // before first login). Adopt it by swapping in the real id below.
            if (user is null)
            {
                user = await dbContext.Users
                    .FirstOrDefaultAsync(u => u.TwitchLogin.ToLower() == loginLower
                        && u.TwitchId.StartsWith(PendingUserMarker.Prefix), ct);
            }

            // Bootstrap: with no admins yet, a login matching `Admin:BootstrapTwitchLogin`
            // is promoted to Admin and allowlisted. This is the only way to create the
            // first admin without manual database edits; the env var can be removed once
            // that admin exists.
            var bootstrapLogin = configuration["Admin:BootstrapTwitchLogin"]?.Trim().ToLowerInvariant();
            var anyAdminExists = await dbContext.Users.AnyAsync(u => u.Role == UserRole.Admin, ct);
            var isBootstrapAdmin = !anyAdminExists
                && !string.IsNullOrEmpty(bootstrapLogin)
                && bootstrapLogin == loginLower;

            var allowlisted = isBootstrapAdmin
                || (user is not null && user.IsAllowlisted)
                || await dbContext.AllowlistedTwitchLogins
                    .AnyAsync(a => a.TwitchLogin == loginLower, ct);

            if (!allowlisted)
            {
                logger.TwitchLoginRejected(loginLower, twitchUser.Id);
                operation.SetError(DiagnosticsConfig.OperationStatuses.NotAllowlisted);
                throw new NotAllowlistedException(twitchUser.Login);
            }

            var created = user is null;
            if (user is null)
            {
                user = new User
                {
                    TwitchId = twitchUser.Id,
                    TwitchLogin = twitchUser.Login,
                    DisplayName = twitchUser.DisplayName,
                    Email = twitchUser.Email,
                    ProfileImageUrl = twitchUser.ProfileImageUrl,
                    IsAllowlisted = true,
                    Role = isBootstrapAdmin ? UserRole.Admin : UserRole.User,
                };
                dbContext.Users.Add(user);
            }
            else
            {
                if (PendingUserMarker.IsPending(user.TwitchId))
                    user.TwitchId = twitchUser.Id;
                user.TwitchLogin = twitchUser.Login;
                user.DisplayName = twitchUser.DisplayName;
                user.Email = twitchUser.Email;
                user.ProfileImageUrl = twitchUser.ProfileImageUrl;
                user.IsAllowlisted = true;
                if (isBootstrapAdmin && user.Role != UserRole.Admin)
                    user.Role = UserRole.Admin;
                user.UpdatedAt = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            var action = created ? DiagnosticsConfig.LogActions.Created : DiagnosticsConfig.LogActions.Updated;
            operation.SetTags(
                (DiagnosticsConfig.Tags.UserId, user.Id),
                (DiagnosticsConfig.Tags.UserRole, user.Role.ToString()),
                (DiagnosticsConfig.Tags.UserCreated, created),
                (DiagnosticsConfig.Tags.OperationType, created
                    ? DiagnosticsConfig.OperationTypes.Create
                    : DiagnosticsConfig.OperationTypes.Update));
            logger.TwitchUserUpserted(action, user.Id, loginLower, user.Role.ToString());
            return user;
        }
        catch (Exception ex) when (ex is not NotAllowlistedException)
        {
            operation.SetError(DiagnosticsConfig.OperationStatuses.Failure, ex.Message, ex);
            logger.TwitchUserUpsertFailed(ex, loginLower, twitchUser.Id);
            throw;
        }
    }
}

public class NotAllowlistedException(string twitchLogin) : Exception($"Twitch login '{twitchLogin}' is not on the allowlist.")
{
    public string TwitchLogin { get; } = twitchLogin;
}

public record TwitchTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string RefreshToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("token_type")] string TokenType);

public record TwitchUserInfo(
    string Id,
    string Login,
    string DisplayName,
    string? Email,
    string? ProfileImageUrl);
