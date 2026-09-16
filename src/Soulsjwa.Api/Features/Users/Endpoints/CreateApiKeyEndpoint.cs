using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Diagnostics;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events;
using Soulsjwa.Api.Infrastructure.Auth;
using Soulsjwa.Api.Infrastructure.Data;
using Soulsjwa.Api.Common;

namespace Soulsjwa.Api.Features.Users.Endpoints;

public sealed record CreateApiKeyRequest(string Name, DateTimeOffset? ExpiresAt);
public sealed record CreateApiKeyResponse(Guid Id, string Name, string Key, string KeyPrefix, DateTime CreatedAt);

public class CreateApiKeyEndpoint : IEndpoint
{
    private const int MaxNameLength = 100;

    /// <summary>
    /// Ceiling on un-revoked, un-expired keys per user. Each key is a permanent
    /// credential and a permanent row, so this bounds an otherwise unbounded
    /// credential-minting endpoint — revoke an old key to make room.
    /// </summary>
    private const int MaxActiveKeysPerUser = 10;

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost(ApiRoutes.Prefix + "/users/me/api-keys", Handle)
            .WithName("CreateApiKey")
            .WithSummary("Creates a new API key for the current user")
            .Produces<CreateApiKeyResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization();
    }

    private static async Task<IResult> Handle(
        CreateApiKeyRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        ILogger<CreateApiKeyEndpoint> logger,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > MaxNameLength)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Name"] = [$"Name is required and must be {MaxNameLength} characters or fewer."],
            });

        if (request.ExpiresAt.HasValue && request.ExpiresAt.Value <= DateTimeOffset.UtcNow)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["ExpiresAt"] = ["Expiration date must be in the future."],
            });

        var userId = EventOwnership.GetUserId(principal);

        var now = DateTime.UtcNow;
        var activeKeyCount = await db.ApiKeys.CountAsync(
            k => k.UserId == userId && !k.IsRevoked && (k.ExpiresAt == null || k.ExpiresAt > now), ct);
        if (activeKeyCount >= MaxActiveKeysPerUser)
            return Results.Problem(
                detail: $"You already have {MaxActiveKeysPerUser} active API keys. Revoke one before creating another.",
                statusCode: StatusCodes.Status409Conflict);

        var (fullKey, prefix) = ApiKeyAuthHandler.GenerateApiKey();
        var keyHash = ApiKeyAuthHandler.HashApiKey(fullKey);

        var apiKey = new ApiKey
        {
            UserId = userId,
            Name = request.Name.Trim(),
            KeyHash = keyHash,
            KeyPrefix = prefix,
            ExpiresAt = request.ExpiresAt is { } expiresAt ? UtcTime.ToStorage(expiresAt) : null,
        };

        db.ApiKeys.Add(apiKey);
        await db.SaveChangesAsync(ct);
        logger.ApiKeyCreated(apiKey.Id, apiKey.KeyPrefix, userId);

        return Results.Created($"/api/v1/users/me/api-keys/{apiKey.Id}",
            new CreateApiKeyResponse(apiKey.Id, apiKey.Name, fullKey, prefix, apiKey.CreatedAt));
    }
}
