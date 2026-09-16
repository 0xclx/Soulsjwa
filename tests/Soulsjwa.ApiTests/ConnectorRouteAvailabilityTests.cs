using System.Net;
using FluentAssertions;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// Every route the desktop connector calls must exist on a production host.
/// The connector once probed a route that was only mapped in Development, so
/// it could connect to a developer's machine and to nothing else — and no test
/// noticed, because the suite boots Development. This walks the connector's
/// connect sequence against a Production start and accepts any answer except
/// "no such route".
///
/// Kept in step with <c>Soulsjwa.Connector.Services.ApiService</c>: add a row
/// here for every path added there.
/// </summary>
public class ConnectorRouteAvailabilityTests
{
    private const int SupportedGameId = 1;

    public static TheoryData<string, string, HttpStatusCode> ConnectorRoutes => new()
    {
        { "GET", "/api/v1/connector/version", HttpStatusCode.OK },
        { "GET", "/api/v1/connector/supported-games", HttpStatusCode.OK },
        { "GET", $"/api/v1/connector/games/{SupportedGameId}/data", HttpStatusCode.Unauthorized },
        { "GET", "/api/v1/connector/events", HttpStatusCode.Unauthorized },
        { "POST", $"/api/v1/connector/events/{Guid.NewGuid()}/games/{Guid.NewGuid()}/submit", HttpStatusCode.Unauthorized },
    };

    [Theory]
    [MemberData(nameof(ConnectorRoutes))]
    public async Task ConnectorRoute_ExistsOnAProductionHost(string method, string path, HttpStatusCode expected)
    {
        await ProductionHost.RunAsync(async client =>
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), path);
            if (method == "POST") request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request);

            response.StatusCode.Should().Be(expected,
                "the connector calls {0} {1} in every environment; a 404 here means the route is mapped conditionally",
                method, path);
        });
    }

    [Fact]
    public async Task DevelopmentOnlyRoutes_AreNotMappedInProduction()
    {
        await ProductionHost.RunAsync(async client =>
        {
            // Swagger is the one Development-only surface left; the connector
            // must never depend on anything mapped the same way.
            var response = await client.GetAsync("/swagger/v1/swagger.json");
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        });
    }
}
