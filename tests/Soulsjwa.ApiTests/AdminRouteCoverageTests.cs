using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Soulsjwa.Api.Common;
using Soulsjwa.Api.Features.Auth.Entities;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// Admin-only routes are gated declaratively (<see cref="AdminAccess.RequireAdmin"/>)
/// rather than by a check each handler has to remember. This test is the
/// other half of that: the list below is every route that must be admin-only,
/// the marker on each is verified through the endpoint table, and each route
/// is called anonymously and as a plain user. Adding an admin route means
/// adding it here; dropping the gate from one fails here.
/// </summary>
public partial class AdminRouteCoverageTests : ApiTestBase
{
    private static readonly (string Method, string Route)[] ExpectedAdminOnly =
    [
        ("GET", "/api/v1/admin/users"),
        ("PATCH", "/api/v1/admin/users/{id}/role"),
        ("GET", "/api/v1/admin/allowlist"),
        ("POST", "/api/v1/admin/allowlist"),
        ("DELETE", "/api/v1/admin/allowlist/{id}"),
        ("GET", "/api/v1/admin/feature-flags/{key}"),
        ("PUT", "/api/v1/admin/feature-flags/{key}"),
        ("GET", "/api/v1/admin/audits"),
        ("POST", "/api/v1/games"),
        ("PATCH", "/api/v1/games/{gameId}"),
        ("PUT", "/api/v1/legal/{kind}"),
        ("POST", "/api/v1/uploads"),
        ("PUT", "/api/v1/theme"),
        ("POST", "/api/v1/objectives/predefined"),
        ("POST", "/api/v1/events"),
        ("POST", "/api/v1/events/{id}/feature"),
        ("POST", "/api/v1/events/{id}/unfeature"),
        ("POST", "/api/v1/events/{id}/duplicate"),
        ("GET", "/api/v1/admin/twitch-extension"),
        ("PUT", "/api/v1/admin/twitch-extension/settings"),
        ("GET", "/api/v1/admin/twitch-extension/bundle"),
    ];

    public static TheoryData<string, string> AdminRoutes
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var (method, route) in ExpectedAdminOnly) data.Add(method, route);
            return data;
        }
    }

    [Fact]
    public void TheAdminOnlyMarker_IsOnExactlyTheExpectedRoutes()
    {
        var marked = Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<AdminOnlyAttribute>() is not null)
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["GET"])
                .Select(m => (Method: m, Route: Normalize(e.RoutePattern.RawText!))))
            .ToHashSet();

        marked.Should().BeEquivalentTo(ExpectedAdminOnly,
            "every admin-only route carries the marker and no other route does; update the list when adding one");
    }

    [Theory]
    [MemberData(nameof(AdminRoutes))]
    public async Task AdminRoute_RefusesAnonymousAndNonAdminCallers(string method, string route)
    {
        var (_, userKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "plain", UserRole.User);
        using var userClient = TestAuth.CreateAuthenticatedClient(Factory, userKey);
        var path = Instantiate(route);

        using var anonymousRequest = Request(method, path);
        var anonymous = await Client.SendAsync(anonymousRequest);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "{0} {1} anonymously", method, path);

        using var userRequest = Request(method, path);
        var asUser = await userClient.SendAsync(userRequest);
        asUser.StatusCode.Should().Be(HttpStatusCode.Forbidden, "{0} {1} as a non-admin", method, path);
        var problem = await asUser.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Detail.Should().Be(AdminAccess.ForbiddenDetail);
    }

    private static HttpRequestMessage Request(string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST" or "PUT" or "PATCH")
            request.Content = JsonContent.Create(new { });
        return request;
    }

    private static string Instantiate(string route) => route
        .Replace("{id}", Guid.NewGuid().ToString())
        .Replace("{gameId}", "1")
        .Replace("{key}", "some-flag")
        .Replace("{kind}", "Impressum");

    /// <summary>
    /// Drops route constraints so "{id:guid}" and "{id}" compare equal, and the
    /// trailing slash a group's "/" route carries.
    /// </summary>
    private static string Normalize(string rawText) =>
        ConstraintPattern().Replace(rawText, "{$1}").TrimEnd('/');

    [GeneratedRegex(@"\{([^}:]+):[^}]+\}")]
    private static partial Regex ConstraintPattern();

    private sealed record ProblemDto(string? Detail);
}
