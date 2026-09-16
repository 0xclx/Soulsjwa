namespace Soulsjwa.Api.Features.Auth;

public static class RefreshTokenCookie
{
    public const string Name = "refresh_token";

    public static CookieOptions CreateOptions(DateTime expiresAtUtc) =>
        new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            IsEssential = true,
            Path = "/api/v1/auth",
            Expires = new DateTimeOffset(expiresAtUtc),
        };

    public static CookieOptions CreateDeletionOptions() =>
        new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            IsEssential = true,
            Path = "/api/v1/auth",
        };
}
