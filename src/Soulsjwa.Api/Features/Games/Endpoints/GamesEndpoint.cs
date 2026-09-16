using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Common.Interfaces;
using Soulsjwa.Api.Features.Admin.Endpoints;
using Soulsjwa.Api.Features.Events;
using Soulsjwa.Api.Features.Games.Definitions;
using Soulsjwa.Api.Features.Games.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Soulsjwa.Shared;
using Soulsjwa.Api.Common;

namespace Soulsjwa.Api.Features.Games.Endpoints;

public sealed record GameResponse(int Id, string Name, string Description, bool ConnectorSupported, string? RequiredConnectorVersion);

/// <summary>
/// Display-oriented data-point metadata for the web rule builder. Deliberately
/// omits Offset, SourceId and ReaderCapability: connector implementation details
/// the browser doesn't need, served only via the authenticated connector endpoint.
/// </summary>
public sealed record GameDataPointResponse(
    string Id,
    string DisplayName,
    string Description,
    string Unit,
    GameDataCategory Category,
    GameDataValueKind ValueKind,
    IReadOnlyList<GameDataComparisonOperator> AllowedComparisons);

public sealed record CreateGameRequest(string Name, string? Description);
public sealed record UpdateGameRequest(string? Name, string? Description);

public class GamesEndpoint : IEndpoint
{
    private const int MaxNameLength = 200;
    private const int MaxDescriptionLength = 1000;

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.Prefix + "/games", Handle)
            .WithName("GetGames")
            .WithSummary("Lists all known games")
            .Produces<List<GameResponse>>(StatusCodes.Status200OK)
            .AllowAnonymous();

        app.MapGet(ApiRoutes.Prefix + "/games/{gameId:int}/data-definitions", GetDataDefinitions)
            .WithName("GetGameDataDefinitions")
            .WithSummary("Lists public data point definitions for a game")
            .Produces<List<GameDataPointResponse>>(StatusCodes.Status200OK)
            .AllowAnonymous();

        app.MapPost(ApiRoutes.Prefix + "/games", Create)
            .WithName("CreateGame")
            .WithSummary("Creates a game in the global catalog (admin only)")
            .Produces<GameResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAdmin();

        app.MapPatch(ApiRoutes.Prefix + "/games/{gameId:int}", Update)
            .WithName("UpdateGame")
            .WithSummary("Updates game catalog metadata (admin only)")
            .Produces<GameResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAdmin();
    }

    private static async Task<IResult> Handle(AppDbContext db, CancellationToken ct)
    {
        var games = await db.Games
            .OrderBy(g => g.Name)
            .Select(g => new GameResponse(g.Id, g.Name, g.Description, g.ConnectorSupported, g.RequiredConnectorVersion))
            .ToListAsync(ct);

        return Results.Ok(games);
    }

    private static IResult GetDataDefinitions(int gameId)
    {
        var dataPoints = GameDataDefinitions.ForGame(gameId)
            .Select(dp => new GameDataPointResponse(
                dp.Id, dp.DisplayName, dp.Description, dp.Unit, dp.Category, dp.ValueKind, dp.AllowedComparisons))
            .ToList();

        return Results.Ok(dataPoints);
    }

    private static async Task<IResult> Create(
        CreateGameRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken ct)
    {
        if (!EventOwnership.IsAdmin(principal)) return AdminAccess.Forbid();

        var errors = Validate(request.Name, request.Description);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var name = request.Name.Trim();
        if (await db.Games.AnyAsync(g => g.Name.ToLower() == name.ToLower(), ct))
            return Results.Problem(
                detail: "A game with this name already exists.",
                statusCode: StatusCodes.Status409Conflict);

        var game = new Game
        {
            Name = name,
            Description = request.Description?.Trim() ?? string.Empty
        };
        db.Games.Add(game);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/v1/games/{game.Id}", ToResponse(game));
    }

    private static async Task<IResult> Update(
        int gameId,
        UpdateGameRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken ct)
    {
        if (!EventOwnership.IsAdmin(principal)) return AdminAccess.Forbid();
        if (request.Name is null && request.Description is null)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Request"] = ["At least one field must be provided."]
            });

        var errors = Validate(request.Name, request.Description);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var game = await db.Games.FirstOrDefaultAsync(g => g.Id == gameId, ct);
        if (game is null)
            return Results.Problem(detail: "Game not found.", statusCode: StatusCodes.Status404NotFound);

        if (request.Name is not null)
        {
            var name = request.Name.Trim();
            if (await db.Games.AnyAsync(g => g.Id != gameId && g.Name.ToLower() == name.ToLower(), ct))
                return Results.Problem(
                    detail: "A game with this name already exists.",
                    statusCode: StatusCodes.Status409Conflict);
            game.Name = name;
        }

        if (request.Description is not null) game.Description = request.Description.Trim();

        await db.SaveChangesAsync(ct);
        return Results.Ok(ToResponse(game));
    }

    private static Dictionary<string, string[]> Validate(string? name, string? description)
    {
        var errors = new Dictionary<string, string[]>();
        if (name is not null)
        {
            if (string.IsNullOrWhiteSpace(name))
                errors["Name"] = ["Name is required."];
            else if (name.Trim().Length > MaxNameLength)
                errors["Name"] = [$"Name must be {MaxNameLength} characters or fewer."];
        }

        if (description?.Trim().Length > MaxDescriptionLength)
            errors["Description"] = [$"Description must be {MaxDescriptionLength} characters or fewer."];

        return errors;
    }

    private static GameResponse ToResponse(Game game) =>
        new(game.Id, game.Name, game.Description, game.ConnectorSupported, game.RequiredConnectorVersion);
}
