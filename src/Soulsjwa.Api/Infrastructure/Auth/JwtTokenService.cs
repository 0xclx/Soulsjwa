using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Soulsjwa.Api.Diagnostics;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.Api.Infrastructure.Auth;

public class JwtTokenService(IConfiguration configuration, AppDbContext dbContext, ILogger<JwtTokenService>? logger = null)
{
    private readonly string _secret = configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT secret not configured");
    private readonly string _issuer = configuration["Jwt:Issuer"] ?? "soulsjwa";
    private readonly string _audience = configuration["Jwt:Audience"] ?? "soulsjwa";
    private readonly int _accessTokenMinutes = int.Parse(configuration["Jwt:AccessTokenMinutes"] ?? "15");
    private readonly int _refreshTokenDays = int.Parse(configuration["Jwt:RefreshTokenDays"] ?? "30");

    public string GenerateAccessToken(User user)
    {
        using var operation = DiagnosticsConfig.StartBusinessOperation(
            DiagnosticsConfig.BusinessOperationNames.JwtGenerateAccessToken,
            DiagnosticsConfig.ActivityNames.JwtGenerateAccessToken);
        operation.Activity?.SetTag(DiagnosticsConfig.Tags.UserId, user.Id);
        operation.Activity?.SetTag(DiagnosticsConfig.Tags.UserRole, user.Role.ToString());

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _issuer,
            Audience = _audience,
            Expires = DateTime.UtcNow.AddMinutes(_accessTokenMinutes),
            SigningCredentials = creds,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
                ["twitch_login"] = user.TwitchLogin,
                ["display_name"] = user.DisplayName,
                ["role"] = user.Role.ToString(),
            },
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        logger?.JwtAccessTokenGenerated(user.Id, user.Role.ToString());
        return token;
    }

    /// <summary>Only the token's hash is stored; the raw token is returned and never persisted.</summary>
    public async Task<(string RawToken, RefreshToken Entity)> GenerateRefreshTokenAsync(Guid userId, CancellationToken ct = default)
    {
        using var operation = DiagnosticsConfig.StartBusinessOperation(
            DiagnosticsConfig.BusinessOperationNames.JwtGenerateRefreshToken,
            DiagnosticsConfig.ActivityNames.JwtGenerateRefreshToken);
        operation.Activity?.SetTag(DiagnosticsConfig.Tags.UserId, userId);

        var tokenBytes = new byte[64];
        RandomNumberGenerator.Fill(tokenBytes);
        var rawToken = Convert.ToBase64String(tokenBytes);
        var prefix = rawToken[..Math.Min(8, rawToken.Length)];
        var hash = HashToken(rawToken);

        var refreshToken = new RefreshToken
        {
            UserId = userId,
            TokenHash = hash,
            TokenPrefix = prefix,
            ExpiresAt = DateTime.UtcNow.AddDays(_refreshTokenDays),
        };

        try
        {
            dbContext.RefreshTokens.Add(refreshToken);
            await dbContext.SaveChangesAsync(ct);
            operation.Activity?.SetTag(DiagnosticsConfig.Tags.RefreshTokenId, refreshToken.Id);
            logger?.JwtRefreshTokenGenerated(userId, prefix);
            return (rawToken, refreshToken);
        }
        catch (Exception ex)
        {
            operation.SetError(DiagnosticsConfig.OperationStatuses.Failure, ex.Message, ex);
            logger?.JwtRefreshTokenGenerationFailed(ex, userId);
            throw;
        }
    }

    public async Task<(User? User, RefreshToken? Token)> ValidateRefreshTokenAsync(string rawToken, CancellationToken ct = default)
    {
        using var operation = DiagnosticsConfig.StartBusinessOperation(
            DiagnosticsConfig.BusinessOperationNames.JwtValidateRefreshToken,
            DiagnosticsConfig.ActivityNames.JwtValidateRefreshToken);
        var prefix = rawToken[..Math.Min(8, rawToken.Length)];
        var hash = HashToken(rawToken);

        var refreshToken = await dbContext.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenPrefix == prefix && t.TokenHash == hash, ct);

        if (refreshToken is null)
        {
            logger?.JwtRefreshTokenUnknown(prefix);
            operation.SetStatus(DiagnosticsConfig.OperationStatuses.NotFound);
            return (null, null);
        }

        operation.Activity?.SetTag(DiagnosticsConfig.Tags.UserId, refreshToken.UserId);

        // Refresh-token reuse detection (RFC 6749 §10.4 / OAuth 2.0 Security BCP §4.13):
        // a previously-rotated (i.e. revoked) token presented again is treated as proof
        // of theft, so every active token for the user is revoked. Logging the
        // legitimate owner out is the documented mitigation for replay.
        if (refreshToken.IsRevoked && !refreshToken.IsExpired)
        {
            logger?.JwtRefreshTokenReuseDetected(refreshToken.UserId, refreshToken.TokenPrefix);
            await RevokeAllForUserAsync(refreshToken.UserId, ct);
            operation.SetStatus(DiagnosticsConfig.OperationStatuses.ReuseDetected);
            return (null, null);
        }

        if (!refreshToken.IsActive)
        {
            logger?.JwtInactiveRefreshTokenRejected(refreshToken.UserId, prefix);
            operation.SetStatus(DiagnosticsConfig.OperationStatuses.Inactive);
            return (null, null);
        }

        return (refreshToken.User, refreshToken);
    }

    /// <summary>Called on reuse detection, to invalidate any sibling token an attacker may already hold.</summary>
    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        using var operation = DiagnosticsConfig.StartBusinessOperation(
            DiagnosticsConfig.BusinessOperationNames.JwtRevokeAllRefreshTokens,
            DiagnosticsConfig.ActivityNames.JwtRevokeAllForUser);
        operation.Activity?.SetTag(DiagnosticsConfig.Tags.UserId, userId);
        var now = DateTime.UtcNow;
        var tokens = await dbContext.RefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .ToListAsync(ct);

        foreach (var t in tokens)
        {
            t.IsRevoked = true;
            t.RevokedAt = now;
        }

        if (tokens.Count > 0)
            await dbContext.SaveChangesAsync(ct);

        operation.Activity?.SetTag(DiagnosticsConfig.Tags.RefreshTokensRevoked, tokens.Count);
        logger?.JwtRefreshTokensRevoked(tokens.Count, userId);
    }

    /// <summary>
    /// Returns <c>false</c> when a concurrent rotation already claimed the token —
    /// the caller must abort with 401 rather than issue a second token chain for
    /// the same cookie.
    /// </summary>
    public async Task<bool> TryRevokeForRotationAsync(Guid tokenId, CancellationToken ct = default)
    {
        var affected = await dbContext.RefreshTokens
            .Where(t => t.Id == tokenId && !t.IsRevoked)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.IsRevoked, true)
                .SetProperty(t => t.RevokedAt, DateTime.UtcNow), ct);
        return affected == 1;
    }

    public async Task RevokeRefreshTokenAsync(string rawToken, CancellationToken ct = default)
    {
        using var operation = DiagnosticsConfig.StartBusinessOperation(
            DiagnosticsConfig.BusinessOperationNames.JwtRevokeRefreshToken,
            DiagnosticsConfig.ActivityNames.JwtRevokeRefreshToken);
        var prefix = rawToken[..Math.Min(8, rawToken.Length)];
        var hash = HashToken(rawToken);

        var refreshToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenPrefix == prefix && t.TokenHash == hash, ct);

        if (refreshToken is not null)
        {
            operation.Activity?.SetTag(DiagnosticsConfig.Tags.UserId, refreshToken.UserId);
            refreshToken.IsRevoked = true;
            refreshToken.RevokedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
            logger?.JwtRefreshTokenRevoked(refreshToken.UserId, prefix);
        }

        if (refreshToken is null)
            operation.SetStatus(DiagnosticsConfig.OperationStatuses.NotFound);
    }

    public TokenValidationParameters GetValidationParameters() =>
        BuildValidationParameters(_secret, _issuer, _audience);

    /// <summary>
    /// Shared by this service at runtime and <c>Program.cs</c> at startup so the two
    /// never drift out of sync.
    /// </summary>
    public static TokenValidationParameters BuildValidationParameters(IConfiguration configuration)
    {
        var secret = configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret must be configured.");
        var issuer = configuration["Jwt:Issuer"] ?? "soulsjwa";
        var audience = configuration["Jwt:Audience"] ?? "soulsjwa";
        return BuildValidationParameters(secret, issuer, audience);
    }

    private static TokenValidationParameters BuildValidationParameters(string secret, string issuer, string audience) =>
        new()
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };

    private static string HashToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hash);
    }

}
