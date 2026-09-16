using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// Reads a minimal-API handler's <see cref="IResult"/> without a round trip.
/// Every result type minimal APIs produce implements
/// <see cref="IStatusCodeHttpResult"/>, so one helper covers all of them.
/// </summary>
public static class HandlerHarness
{
    /// <summary>
    /// The status code this result would have produced; 200 when it carries
    /// none, which is what <see cref="Results.Ok()"/> and friends leave for the
    /// framework to default.
    /// </summary>
    public static int Status(this IResult result) => result switch
    {
        IStatusCodeHttpResult s => s.StatusCode ?? StatusCodes.Status200OK,
        _ => StatusCodes.Status200OK,
    };

    /// <summary>
    /// The <c>detail</c> of a problem response, or null for anything else.
    /// This is the text the frontend surfaces.
    /// </summary>
    public static string? Detail(this IResult result) => result switch
    {
        ProblemHttpResult p => p.ProblemDetails.Detail,
        _ => null,
    };

    public static IDictionary<string, string[]>? ValidationErrors(this IResult result) => result switch
    {
        ProblemHttpResult { ProblemDetails: HttpValidationProblemDetails v } => v.Errors,
        _ => null,
    };

    /// <summary>
    /// The payload of a successful result, so a wrong-shaped answer fails here
    /// rather than as a null reference later.
    /// </summary>
    /// <remarks>
    /// Matched on the runtime value rather than <c>IValueHttpResult&lt;T&gt;</c>:
    /// <c>Results.Ok(items.Select(...))</c> produces an
    /// <c>Ok&lt;IEnumerable&lt;T&gt;&gt;</c> over an iterator, and the caller
    /// should not have to name that type to read it.
    /// </remarks>
    public static T Value<T>(this IResult result) => result switch
    {
        IValueHttpResult v => v.Value switch
        {
            T typed => typed,
            null => throw new InvalidOperationException($"Result carried a null {typeof(T).Name}."),
            var other => throw new InvalidOperationException(
                $"Expected a {typeof(T).Name} payload but the result carried {other.GetType().Name}."),
        },
        _ => throw new InvalidOperationException(
            $"Expected a value result but got {result.GetType().Name} " +
            $"(status {result.Status()}){FormatDetail(result)}."),
    };

    private static string FormatDetail(IResult result) =>
        result.Detail() is { } detail ? $": {detail}" : string.Empty;

    /// <summary>
    /// The principal the auth schemes would have built: the id as
    /// <see cref="ClaimTypes.NameIdentifier"/> and the role under the raw
    /// <c>role</c> name the API-key handler uses.
    /// </summary>
    public static ClaimsPrincipal Principal(this User user) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(EventOwnership.RoleClaim, user.Role.ToString()),
            ],
            authenticationType: "Test"));

    public static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());
}

/// <summary>
/// Stands in for the output-cache store, recording the tags handlers evict on
/// write. Whether a cache actually forgot anything is the API suite's business,
/// since output caching only exists inside the HTTP pipeline.
/// </summary>
public sealed class RecordingCacheStore : IOutputCacheStore
{
    private readonly List<string> _evicted = [];

    /// <summary>Cache tags evicted so far, in order.</summary>
    public IReadOnlyList<string> Evicted => _evicted;

    public ValueTask EvictByTagAsync(string tag, CancellationToken cancellationToken)
    {
        lock (_evicted) _evicted.Add(tag);
        return ValueTask.CompletedTask;
    }

    public ValueTask<byte[]?> GetAsync(string key, CancellationToken cancellationToken) =>
        ValueTask.FromResult<byte[]?>(null);

    public ValueTask SetAsync(
        string key, byte[] value, string[]? tags, TimeSpan validFor, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;
}
